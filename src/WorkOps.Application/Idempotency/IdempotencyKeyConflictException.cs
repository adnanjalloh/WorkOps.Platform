namespace WorkOps.Application.Idempotency;

public sealed class IdempotencyKeyConflictException : Exception
{
    public IdempotencyKeyConflictException()
        : base("The idempotency key was already used for a different request.")
    {
    }
}
