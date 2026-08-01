using WorkOps.Application.Tenancy;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Application.Abstractions;

public interface IWorkspaceStore
{
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    void Add(Workspace workspace);

    void Add(WorkspaceMembership membership);

    Task<Workspace?> GetCurrentAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceMemberView>> ListCurrentMembersAsync(CancellationToken cancellationToken);
}
