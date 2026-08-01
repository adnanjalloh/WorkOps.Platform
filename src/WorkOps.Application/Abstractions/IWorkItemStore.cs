using WorkOps.Application.WorkItems;
using WorkOps.Domain.WorkItems;

namespace WorkOps.Application.Abstractions;

public interface IWorkItemStore
{
    void Add(WorkItem workItem);

    Task<WorkItem?> FindAsync(Guid workItemId, CancellationToken cancellationToken);

    Task<WorkItemView?> GetAsync(Guid workItemId, CancellationToken cancellationToken);
}
