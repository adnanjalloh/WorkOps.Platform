using WorkOps.Application.Messaging;
using WorkOps.Domain.Messaging;

namespace WorkOps.Application.Abstractions;

public interface IOutboxStore
{
    void Add(OutboxMessage message);

    Task<OutboxLease?> LeaseNextAsync(
        DateTimeOffset now,
        DateTimeOffset lockedUntil,
        CancellationToken cancellationToken);

    Task MarkProcessedAsync(
        Guid messageId,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken);

    Task MarkPublishFailureAsync(
        Guid messageId,
        DateTimeOffset failedAt,
        DateTimeOffset nextAttemptAt,
        int maximumAttempts,
        string errorCode,
        CancellationToken cancellationToken);

    Task<OutboxMessage?> FindCurrentAsync(
        Guid messageId,
        CancellationToken cancellationToken);
}
