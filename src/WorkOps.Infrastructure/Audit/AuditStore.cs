using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common.Pagination;
using WorkOps.Domain.Audit;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Audit;

internal sealed class AuditStore(WorkOpsDbContext dbContext) : IAuditStore
{
    public void Add(AuditEvent auditEvent) => dbContext.AuditEvents.Add(auditEvent);

    public async Task<PagedResult<AuditEventView>> ListAsync(
        int page,
        int pageSize,
        string? action,
        string? entityType,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AuditEvents.AsNoTracking();
        if (action is not null)
        {
            query = query.Where(auditEvent => auditEvent.Action == action);
        }

        if (entityType is not null)
        {
            query = query.Where(auditEvent => auditEvent.EntityType == entityType);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .ThenByDescending(auditEvent => auditEvent.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(auditEvent => new AuditEventView(
                auditEvent.Id,
                auditEvent.ActorUserId,
                auditEvent.Action,
                auditEvent.EntityType,
                auditEvent.EntityId,
                auditEvent.OccurredAt,
                auditEvent.CorrelationId,
                auditEvent.MetadataJson))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<AuditEventView>(items, page, pageSize, totalCount);
    }
}
