using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Domain.Idempotency;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Idempotency;

internal sealed class IdempotencyStore(WorkOpsDbContext dbContext) : IIdempotencyStore
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
}
