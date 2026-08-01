using WorkOps.Domain.Common;

namespace WorkOps.Domain.Messaging;

public sealed class OutboxMessage : IWorkspaceOwned
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        WorkspaceId workspaceId,
        string type,
        string payloadJson,
        DateTimeOffset occurredAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Type = type;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        Status = OutboxMessageStatus.Pending;
        NextAttemptAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public WorkspaceId WorkspaceId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public OutboxMessageStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public string? LastErrorCode { get; private set; }

    public static OutboxMessage Create(
        Guid id,
        WorkspaceId workspaceId,
        string type,
        string payloadJson,
        DateTimeOffset occurredAt) => new(id, workspaceId, type, payloadJson, occurredAt);

    public void Lease(DateTimeOffset lockedUntil)
    {
        Status = OutboxMessageStatus.Processing;
        AttemptCount++;
        LockedUntil = lockedUntil;
        LastErrorCode = null;
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = processedAt;
        LockedUntil = null;
        LastErrorCode = null;
    }

    public void MarkPublishFailure(
        DateTimeOffset failedAt,
        DateTimeOffset nextAttemptAt,
        int maximumAttempts,
        string errorCode)
    {
        LockedUntil = null;
        LastErrorCode = errorCode;

        if (AttemptCount >= maximumAttempts)
        {
            Status = OutboxMessageStatus.Failed;
            FailedAt = failedAt;
            return;
        }

        Status = OutboxMessageStatus.Pending;
        NextAttemptAt = nextAttemptAt;
    }

    public void Replay(DateTimeOffset nextAttemptAt)
    {
        if (Status != OutboxMessageStatus.Failed)
        {
            throw new OutboxReplayNotAllowedException();
        }

        Status = OutboxMessageStatus.Pending;
        AttemptCount = 0;
        NextAttemptAt = nextAttemptAt;
        LockedUntil = null;
        FailedAt = null;
        LastErrorCode = null;
    }
}
