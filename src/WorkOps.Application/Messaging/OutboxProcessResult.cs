namespace WorkOps.Application.Messaging;

public enum OutboxProcessResult
{
    NoMessage,
    Published,
    RetryScheduled,
    Failed,
}
