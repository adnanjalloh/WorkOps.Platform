namespace WorkOps.Domain.Messaging;

public sealed class OutboxReplayNotAllowedException : Exception
{
    public OutboxReplayNotAllowedException()
        : base("Only failed outbox messages can be replayed.")
    {
    }
}
