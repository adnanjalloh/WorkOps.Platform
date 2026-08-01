using WorkOps.Application.Abstractions;
using WorkOps.Application.Messaging;
using WorkOps.Domain;
using WorkOps.Domain.Messaging;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class OutboxProcessorTests
{
    [TestMethod]
    public async Task Successful_publish_marks_the_message_processed()
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var store = new RecordingOutboxStore(CreateLease(attemptCount: 1));
        var processor = new OutboxProcessor(store, new SuccessfulPublisher(), new FixedTimeProvider(now));

        var result = await processor.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(OutboxProcessResult.Published, result);
        Assert.IsTrue(store.Processed);
        Assert.IsFalse(store.PublishFailed);
    }

    [TestMethod]
    public async Task Publish_failure_records_only_a_safe_error_code_and_schedules_retry()
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var lease = CreateLease(attemptCount: 2);
        var store = new RecordingOutboxStore(lease);
        var processor = new OutboxProcessor(store, new FailingPublisher(), new FixedTimeProvider(now));

        var result = await processor.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(OutboxProcessResult.RetryScheduled, result);
        Assert.IsTrue(store.PublishFailed);
        Assert.AreEqual("transport_publish_failed", store.ErrorCode);
        Assert.IsGreaterThan(now, store.NextAttemptAt);
    }

    [TestMethod]
    public void Retry_delay_is_deterministic_and_bounded()
    {
        var messageId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        var first = OutboxRetryPolicy.GetDelay(messageId, 1);
        var repeated = OutboxRetryPolicy.GetDelay(messageId, 1);
        var final = OutboxRetryPolicy.GetDelay(messageId, 50);

        Assert.AreEqual(first, repeated);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromSeconds(1), first);
        Assert.IsLessThan(TimeSpan.FromSeconds(2), first);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromSeconds(16), final);
        Assert.IsLessThan(TimeSpan.FromSeconds(17), final);
    }

    private static OutboxLease CreateLease(int attemptCount) => new(
        Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
        WorkspaceId.New(),
        WorkItemStatusChangedMessage.MessageType,
        "{}",
        attemptCount,
        new DateTimeOffset(2026, 8, 1, 11, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SuccessfulPublisher : IMessagePublisher
    {
        public Task PublishAsync(OutboxLease message, CancellationToken cancellationToken)
        {
            _ = message;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingPublisher : IMessagePublisher
    {
        public Task PublishAsync(OutboxLease message, CancellationToken cancellationToken)
        {
            _ = message;
            return Task.FromException(new InvalidOperationException("Sensitive provider detail."));
        }
    }

    private sealed class RecordingOutboxStore(OutboxLease lease) : IOutboxStore
    {
        private bool _leased;

        public bool Processed { get; private set; }

        public bool PublishFailed { get; private set; }

        public string? ErrorCode { get; private set; }

        public DateTimeOffset NextAttemptAt { get; private set; }

        public void Add(OutboxMessage message) => throw new NotSupportedException();

        public Task<OutboxLease?> LeaseNextAsync(
            DateTimeOffset now,
            DateTimeOffset lockedUntil,
            CancellationToken cancellationToken)
        {
            _ = now;
            _ = lockedUntil;
            if (_leased)
            {
                return Task.FromResult<OutboxLease?>(null);
            }

            _leased = true;
            return Task.FromResult<OutboxLease?>(lease);
        }

        public Task MarkProcessedAsync(
            Guid messageId,
            DateTimeOffset processedAt,
            CancellationToken cancellationToken)
        {
            _ = messageId;
            _ = processedAt;
            Processed = true;
            return Task.CompletedTask;
        }

        public Task MarkPublishFailureAsync(
            Guid messageId,
            DateTimeOffset failedAt,
            DateTimeOffset nextAttemptAt,
            int maximumAttempts,
            string errorCode,
            CancellationToken cancellationToken)
        {
            _ = messageId;
            _ = failedAt;
            _ = maximumAttempts;
            PublishFailed = true;
            NextAttemptAt = nextAttemptAt;
            ErrorCode = errorCode;
            return Task.CompletedTask;
        }

        public Task<OutboxMessage?> FindCurrentAsync(
            Guid messageId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
