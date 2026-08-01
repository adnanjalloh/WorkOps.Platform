namespace WorkOps.Application.Idempotency;

public sealed class IdempotencyRaceException : Exception
{
    public IdempotencyRaceException()
        : base("Another request is committing the same idempotency key.")
    {
    }
}
