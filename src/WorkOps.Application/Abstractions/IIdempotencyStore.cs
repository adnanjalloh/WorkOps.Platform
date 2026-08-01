using WorkOps.Domain.Idempotency;

namespace WorkOps.Application.Abstractions;

public interface IIdempotencyStore
{
    void Add(IdempotencyRecord record);

    Task<IdempotencyRecord?> FindCurrentAsync(
        Guid userId,
        string method,
        string route,
        string key,
        CancellationToken cancellationToken);
}
