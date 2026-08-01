namespace WorkOps.Domain.Messaging;

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Processed,
    Failed,
}
