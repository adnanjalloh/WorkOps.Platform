using WorkOps.Application.Common.Pagination;
using WorkOps.Application.Projects;
using WorkOps.Domain.Projects;

namespace WorkOps.Application.Abstractions;

public interface IProjectStore
{
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken);

    void Add(Project project);

    Task<Project?> FindAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ProjectView?> GetAsync(Guid projectId, CancellationToken cancellationToken);

    Task<PagedResult<ProjectView>> ListAsync(
        int page,
        int pageSize,
        string? search,
        ProjectStatus? status,
        CancellationToken cancellationToken);
}
