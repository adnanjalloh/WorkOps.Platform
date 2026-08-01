namespace WorkOps.Application.Abstractions;

public interface IIdempotencyMaintenanceStore
{
    Task<int> PurgeExpiredBatchAsync(
        DateTimeOffset expiresBefore,
        int batchSize,
        CancellationToken cancellationToken);
}
