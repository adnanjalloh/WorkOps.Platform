using WorkOps.Application.Audit;
using WorkOps.Application.Common.Pagination;
using WorkOps.Domain.Audit;

namespace WorkOps.Application.Abstractions;

public interface IAuditStore
{
    void Add(AuditEvent auditEvent);

    Task<PagedResult<AuditEventView>> ListAsync(
        int page,
        int pageSize,
        string? action,
        string? entityType,
        CancellationToken cancellationToken);
}
