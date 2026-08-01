using WorkOps.Application.Abstractions;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Domain;

namespace WorkOps.Application.Tenancy;

public sealed class WorkspaceAccessService(
    IWorkspaceAccessReader accessReader,
    IInputSanitizer sanitizer)
{
    public Task<WorkspaceAccess?> FindAsync(
        string subject,
        WorkspaceId workspaceId,
        CancellationToken cancellationToken)
    {
        var safeSubject = sanitizer.Apply(subject, InputProfile.Identifier, "token.sub");
        return accessReader.FindAsync(safeSubject, workspaceId, cancellationToken);
    }
}
