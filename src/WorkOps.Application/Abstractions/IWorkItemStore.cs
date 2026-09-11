using WorkOps.Application.Common.Pagination;
using WorkOps.Application.WorkItems;
using WorkOps.Domain.WorkItems;

namespace WorkOps.Application.Abstractions;

public interface IWorkItemStore
{
    void Add(WorkItem workItem);

    Task<WorkItem?> FindAsync(Guid workItemId, CancellationToken cancellationToken);

    Task<WorkItemView?> GetAsync(Guid workItemId, CancellationToken cancellationToken);

    Task<PagedResult<WorkItemView>> ListAsync(
        int page,
        int pageSize,
        string? search,
        WorkItemStatus? status,
        Guid? projectId,
        Guid? assigneeUserId,
        CancellationToken cancellationToken);
}
