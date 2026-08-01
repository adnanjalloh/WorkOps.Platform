using WorkOps.Application.Tenancy;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Application.Abstractions;

public interface IWorkspaceStore
{
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    void Add(Workspace workspace);

    void Add(WorkspaceMembership membership);

    Task<WorkspaceMembership?> FindCurrentMembershipAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> IsCurrentMemberActiveAsync(Guid userId, CancellationToken cancellationToken);

    Task<Workspace?> GetCurrentAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceMemberView>> ListCurrentMembersAsync(CancellationToken cancellationToken);
}
