using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Common.Pagination;
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
        WorkItemViews(dbContext.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.Id == workItemId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<WorkItemView>> ListAsync(
        int page,
        int pageSize,
        string? search,
        WorkItemStatus? status,
        Guid? projectId,
        Guid? assigneeUserId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.WorkItems.AsNoTracking();
        if (search is not null)
        {
            // SearchText rejects SQL wildcards; escape the remaining LIKE escape character.
            var pattern = $"%{search.Replace("\\", "\\\\", StringComparison.Ordinal)}%";
            query = query.Where(workItem => EF.Functions.ILike(workItem.Title, pattern, "\\"));
        }

        if (status.HasValue)
        {
            query = query.Where(workItem => workItem.Status == status.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(workItem => workItem.ProjectId == projectId.Value);
        }

        if (assigneeUserId.HasValue)
        {
            query = query.Where(workItem => workItem.AssigneeUserId == assigneeUserId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageQuery = query
            .OrderByDescending(workItem => workItem.CreatedAt)
            .ThenByDescending(workItem => workItem.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
        var items = await WorkItemViews(pageQuery).ToArrayAsync(cancellationToken);
        return new PagedResult<WorkItemView>(items, page, pageSize, totalCount);
    }

    private IQueryable<WorkItemView> WorkItemViews(IQueryable<WorkItem> workItems) =>
        workItems.Select(workItem => new WorkItemView(
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
                workItem.UpdatedAt));
}
