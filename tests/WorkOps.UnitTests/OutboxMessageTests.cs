using WorkOps.Domain;
using WorkOps.Domain.Messaging;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class OutboxMessageTests
{
    [TestMethod]
    public void Publish_failures_are_bounded_and_failed_messages_can_be_replayed()
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var message = OutboxMessage.Create(
            Guid.NewGuid(),
            WorkspaceId.New(),
            "work-item.status-changed.v1",
            "{}",
            now);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            message.Lease(now.AddMinutes(attempt));
            message.MarkPublishFailure(
                now.AddSeconds(attempt),
                now.AddMinutes(attempt),
                5,
                "transport_publish_failed");
        }

        Assert.AreEqual(OutboxMessageStatus.Failed, message.Status);
        Assert.AreEqual(5, message.AttemptCount);
        Assert.AreEqual("transport_publish_failed", message.LastErrorCode);
        Assert.IsNotNull(message.FailedAt);

        message.Replay(now.AddHours(1));

        Assert.AreEqual(OutboxMessageStatus.Pending, message.Status);
        Assert.AreEqual(0, message.AttemptCount);
        Assert.IsNull(message.FailedAt);
        Assert.IsNull(message.LastErrorCode);
    }

    [TestMethod]
    public void Non_failed_message_cannot_be_replayed()
    {
        var message = OutboxMessage.Create(
            Guid.NewGuid(),
            WorkspaceId.New(),
            "work-item.status-changed.v1",
            "{}",
            DateTimeOffset.UtcNow);

        Assert.ThrowsExactly<OutboxReplayNotAllowedException>(
            () => message.Replay(DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void Successful_delivery_clears_the_lease_and_error()
    {
        var now = DateTimeOffset.UtcNow;
        var message = OutboxMessage.Create(
            Guid.NewGuid(),
            WorkspaceId.New(),
            "work-item.status-changed.v1",
            "{}",
            now);
        message.Lease(now.AddMinutes(1));

        message.MarkProcessed(now.AddSeconds(1));

        Assert.AreEqual(OutboxMessageStatus.Processed, message.Status);
        Assert.AreEqual(1, message.AttemptCount);
        Assert.IsNull(message.LockedUntil);
        Assert.AreEqual(now.AddSeconds(1), message.ProcessedAt);
    }
}
