using WorkOps.Application.Abstractions;
using WorkOps.Domain;
using WorkOps.Domain.Identity;

namespace WorkOps.Application.Tenancy;

public sealed class WorkspaceAccessService(IWorkspaceAccessReader accessReader)
{
    public Task<WorkspaceAccess?> FindAsync(
        string subject,
        WorkspaceId workspaceId,
        CancellationToken cancellationToken)
    {
        if (!OidcSubject.IsValid(subject))
        {
            throw new InvalidOperationException("The validated identity subject invariant was not satisfied.");
        }

        return accessReader.FindAsync(subject, workspaceId, cancellationToken);
    }
}
