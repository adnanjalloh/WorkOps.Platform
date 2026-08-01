using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Domain.Idempotency;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Idempotency;

internal sealed class IdempotencyStore(WorkOpsDbContext dbContext)
    : IIdempotencyStore, IIdempotencyMaintenanceStore
{
    public void Add(IdempotencyRecord record) => dbContext.IdempotencyRecords.Add(record);

    public Task<IdempotencyRecord?> FindCurrentAsync(
        Guid userId,
        string method,
        string route,
        string key,
        CancellationToken cancellationToken) => dbContext.IdempotencyRecords
        .SingleOrDefaultAsync(
            record => record.UserId == userId &&
                      record.Method == method &&
                      record.Route == route &&
                      record.Key == key,
            cancellationToken);

    public Task<int> PurgeExpiredBatchAsync(
        DateTimeOffset expiresBefore,
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        // This operator-only retention path deliberately crosses tenant filters. It deletes only
        // expired idempotency rows, uses no submitted values, and is bounded per transaction.
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM idempotency_records
            WHERE ctid IN (
                SELECT ctid
                FROM idempotency_records
                WHERE "ExpiresAt" <= {expiresBefore}
                ORDER BY "ExpiresAt"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
            )
            """, cancellationToken);
    }
}
