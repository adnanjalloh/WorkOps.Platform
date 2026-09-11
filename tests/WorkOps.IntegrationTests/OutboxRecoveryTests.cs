using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Messaging;
using WorkOps.Domain.Messaging;

namespace WorkOps.IntegrationTests;

public sealed partial class TenantQueryFilterTests
{
    [TestMethod]
    public async Task Abandoned_lease_and_failed_completion_recover_without_duplicate_notification()
    {
        var (user, workspace, _) = await SeedProjectAsync("recovery");
        // Keep the virtual clock before the other fixture messages so only this row is due.
        var now = new DateTimeOffset(2000, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var notification = CreateNotificationMessage(Guid.NewGuid(), workspace.Id, user.Id, now);
        var message = OutboxMessage.Create(notification.MessageId, workspace.Id,
            WorkItemStatusChangedMessage.MessageType, MessagePayload.Serialize(notification), now);
        await using (var seed = CreateDbContext(CreateAccessor(user.Id, workspace.Id)))
        {
            seed.OutboxMessages.Add(message);
            await seed.SaveChangesAsync();
        }

        await using var deliveryProvider = CreateServices(enableMessaging: false);
        var completionFailure = new FailFirstCompletionSave(message.Id);
        await using var workerProvider = CreateServices(enableMessaging: false, interceptor: completionFailure);
        var expiry = now + OutboxRetryPolicy.LeaseDuration;

        // A process disappears after committing its lease. A fresh scope must respect its expiry.
        await using (var abandoned = workerProvider.CreateAsyncScope())
        {
            var lease = await abandoned.ServiceProvider.GetRequiredService<IOutboxStore>()
                .LeaseNextAsync(now, expiry, CancellationToken.None);
            Assert.IsNotNull(lease);
            Assert.AreEqual(message.Id, lease.Id);
            Assert.AreEqual(1, lease.AttemptCount);
        }

        await using (var beforeExpiry = workerProvider.CreateAsyncScope())
        {
            var lease = await beforeExpiry.ServiceProvider.GetRequiredService<IOutboxStore>()
                .LeaseNextAsync(expiry.AddMilliseconds(-1), expiry.AddSeconds(30), CancellationToken.None);
            Assert.IsNull(lease);
        }

        var publisher = new PersistingNotificationPublisher(deliveryProvider);
        await using (var recovering = workerProvider.CreateAsyncScope())
        {
            var processor = new OutboxProcessor(recovering.ServiceProvider.GetRequiredService<IOutboxStore>(),
                publisher, new RecoveryTimeProvider(expiry), NullLogger<OutboxProcessor>.Instance);
            Assert.AreEqual(OutboxProcessResult.RetryScheduled, await processor.ProcessNextAsync(CancellationToken.None));
        }

        Assert.IsTrue(completionFailure.Injected);
        Assert.HasCount(1, publisher.DeliveryResults);
        Assert.IsTrue(publisher.DeliveryResults[0]);
        DateTimeOffset retryAt;
        await using (var persisted = CreateDbContext(CreateAccessor(user.Id, workspace.Id)))
        {
            var pending = await persisted.OutboxMessages.SingleAsync(row => row.Id == message.Id);
            Assert.AreEqual(OutboxMessageStatus.Pending, pending.Status);
            Assert.AreEqual(2, pending.AttemptCount);
            Assert.IsNull(pending.LockedUntil);
            Assert.IsNull(pending.ProcessedAt);
            Assert.AreEqual("transport_publish_failed", pending.LastErrorCode);
            retryAt = pending.NextAttemptAt;
            Assert.IsGreaterThan(expiry, retryAt);
        }

        await using (var beforeRetry = workerProvider.CreateAsyncScope())
        {
            var lease = await beforeRetry.ServiceProvider.GetRequiredService<IOutboxStore>()
                .LeaseNextAsync(retryAt.AddMilliseconds(-1), retryAt.AddSeconds(30), CancellationToken.None);
            Assert.IsNull(lease);
        }

        await using (var retry = workerProvider.CreateAsyncScope())
        {
            var processor = new OutboxProcessor(retry.ServiceProvider.GetRequiredService<IOutboxStore>(),
                publisher, new RecoveryTimeProvider(retryAt), NullLogger<OutboxProcessor>.Instance);
            Assert.AreEqual(OutboxProcessResult.Published, await processor.ProcessNextAsync(CancellationToken.None));
        }

        CollectionAssert.AreEqual(new[] { message.Id, message.Id }, publisher.MessageIds.ToArray());
        Assert.HasCount(2, publisher.DeliveryResults);
        Assert.IsTrue(publisher.DeliveryResults[0]);
        Assert.IsFalse(publisher.DeliveryResults[1]);
        await using var verified = CreateDbContext(CreateAccessor(user.Id, workspace.Id));
        var processed = await verified.OutboxMessages.SingleAsync(row => row.Id == message.Id);
        Assert.AreEqual(OutboxMessageStatus.Processed, processed.Status);
        Assert.AreEqual(3, processed.AttemptCount);
        Assert.AreEqual(retryAt, processed.ProcessedAt);
        Assert.IsNull(processed.LockedUntil);
        Assert.IsNull(processed.LastErrorCode);
        Assert.AreEqual(1, await verified.NotificationDeliveries.CountAsync(row => row.SourceMessageId == message.Id));
        Assert.AreEqual(1, await verified.InboxMessages.CountAsync(row => row.MessageId == message.Id));
    }

    private sealed class RecoveryTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PersistingNotificationPublisher(ServiceProvider provider) : IMessagePublisher
    {
        public List<Guid> MessageIds { get; } = [];

        public List<bool> DeliveryResults { get; } = [];

        public async Task PublishAsync(OutboxLease message, CancellationToken cancellationToken)
        {
            await using var consumer = provider.CreateAsyncScope();
            var delivered = await consumer.ServiceProvider.GetRequiredService<NotificationMessageHandler>()
                .HandleAsync(message.Type, message.PayloadJson, cancellationToken);
            MessageIds.Add(message.Id);
            DeliveryResults.Add(delivered);
        }
    }

    private sealed class FailFirstCompletionSave(Guid messageId) : SaveChangesInterceptor
    {
        public bool Injected { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!Injected && eventData.Context!.ChangeTracker.Entries<OutboxMessage>()
                .Any(entry => entry.Entity.Id == messageId && entry.Entity.Status == OutboxMessageStatus.Processed))
            {
                Injected = true;
                throw new IOException("Synthetic completion-write failure");
            }

            return ValueTask.FromResult(result);
        }
    }
}
