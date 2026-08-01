using WorkOps.Application.Identity;
using WorkOps.Application.Tenancy;
using WorkOps.Domain;

namespace WorkOps.Application.Abstractions;

public interface IWorkspaceAccessReader
{
    Task<WorkspaceAccess?> FindAsync(
        string subject,
        WorkspaceId workspaceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MembershipView>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
