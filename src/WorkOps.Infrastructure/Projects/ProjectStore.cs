using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Common.Pagination;
using WorkOps.Application.Projects;
using WorkOps.Domain.Projects;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Projects;

internal sealed class ProjectStore(WorkOpsDbContext dbContext) : IProjectStore
{
    public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(project => project.Key == key, cancellationToken);

    public void Add(Project project) => dbContext.Projects.Add(project);

    public Task<Project?> FindAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

    public Task<ProjectView?> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
        ProjectViews(dbContext.Projects
                .AsNoTracking()
                .Where(project => project.Id == projectId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<ProjectView>> ListAsync(
        int page,
        int pageSize,
        string? search,
        ProjectStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Projects.AsNoTracking();
        if (search is not null)
        {
            var pattern = $"%{search}%";
            query = query.Where(project =>
                EF.Functions.ILike(project.Name, pattern) || project.Key == search);
        }

        if (status.HasValue)
        {
            query = query.Where(project => project.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageQuery = query
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
        var items = await ProjectViews(pageQuery)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<ProjectView>(items, page, pageSize, totalCount);
    }

    private IQueryable<ProjectView> ProjectViews(IQueryable<Project> projects) =>
        projects.Select(project => new ProjectView(
            project.Id,
            project.Name,
            project.Key,
            project.Status,
            dbContext.WorkItems.Count(workItem => workItem.ProjectId == project.Id),
            project.CreatedAt,
            project.UpdatedAt));
}
