using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Identity;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Application.Tenancy;

public sealed class WorkspaceService(
    IdentityService identityService,
    IWorkspaceStore workspaces,
    IUnitOfWork unitOfWork,
    AuditWriter auditWriter,
    IInputSanitizer sanitizer,
    TimeProvider timeProvider)
{
    public async Task<Workspace> CreateAsync(
        CurrentIdentity identity,
        string name,
        string slug,
        CancellationToken cancellationToken)
    {
        var safeName = sanitizer.Apply(name, InputProfile.PlainText, "body.name");
        var safeSlug = sanitizer.Apply(slug, InputProfile.KeyPath, "body.slug");

        if (await workspaces.SlugExistsAsync(safeSlug, cancellationToken))
        {
            throw new DuplicateWorkspaceSlugException();
        }

        var owner = await identityService.GetOrCreateAsync(identity, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var workspace = Workspace.Create(safeName, safeSlug, now);
        var membership = WorkspaceMembership.Create(workspace.Id, owner.Id, WorkspaceRole.Owner, now);

        workspaces.Add(workspace);
        workspaces.Add(membership);
        auditWriter.RecordFor(
            workspace.Id,
            owner.Id,
            AuditActions.WorkspaceCreated,
            "workspace",
            workspace.Id.Value,
            now,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["status"] = workspace.Status.ToString(),
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return workspace;
    }

    public Task<Workspace?> GetCurrentAsync(CancellationToken cancellationToken) =>
        workspaces.GetCurrentAsync(cancellationToken);

    public Task<IReadOnlyList<WorkspaceMemberView>> ListCurrentMembersAsync(
        CancellationToken cancellationToken) => workspaces.ListCurrentMembersAsync(cancellationToken);
}
