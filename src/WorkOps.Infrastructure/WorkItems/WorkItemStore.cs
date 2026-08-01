using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.WorkItems;
using WorkOps.Domain.WorkItems;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.WorkItems;

internal sealed class WorkItemStore(WorkOpsDbContext dbContext) : IWorkItemStore
{
    public void Add(WorkItem workItem) => dbContext.WorkItems.Add(workItem);

    public Task<WorkItem?> FindAsync(Guid workItemId, CancellationToken cancellationToken) =>
        dbContext.WorkItems.SingleOrDefaultAsync(
            workItem => workItem.Id == workItemId,
            cancellationToken);

    public Task<WorkItemView?> GetAsync(Guid workItemId, CancellationToken cancellationToken) =>
        dbContext.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.Id == workItemId)
            .Select(workItem => new WorkItemView(
                workItem.Id,
                workItem.ProjectId,
                workItem.Title,
                workItem.Status,
                workItem.Priority,
                workItem.AssigneeUserId,
                dbContext.Users
                    .Where(user => user.Id == workItem.AssigneeUserId)
                    .Select(user => user.DisplayName)
                    .SingleOrDefault(),
                workItem.Labels,
                workItem.Version,
                workItem.CreatedAt,
                workItem.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
}
