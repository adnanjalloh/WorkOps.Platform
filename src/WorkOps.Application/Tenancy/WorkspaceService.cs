using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Identity;
using WorkOps.Domain.Features;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Application.Tenancy;

public sealed class WorkspaceService(
    IdentityService identityService,
    IWorkspaceStore workspaces,
    IWorkspaceSubscriptionStore subscriptions,
    IUnitOfWork unitOfWork,
    AuditWriter auditWriter,
    IWorkspaceContextAccessor workspaceContext,
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

        return await unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                var owner = await identityService.GetOrCreateAsync(
                    identity,
                    transactionCancellationToken);
                var now = timeProvider.GetUtcNow();
                var workspace = Workspace.Create(safeName, safeSlug, now);
                var membership = WorkspaceMembership.Create(
                    workspace.Id,
                    owner.Id,
                    WorkspaceRole.Owner,
                    now);

                using var provisioning = workspaceContext.BeginProvisioning(workspace.Id);
                workspaces.Add(workspace);
                workspaces.Add(membership);
                subscriptions.Add(WorkspaceSubscription.CreateStarter(workspace.Id, now));
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
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                return workspace;
            },
            cancellationToken);
    }

    public Task<Workspace?> GetCurrentAsync(CancellationToken cancellationToken) =>
        workspaces.GetCurrentAsync(cancellationToken);

    public Task<IReadOnlyList<WorkspaceMemberView>> ListCurrentMembersAsync(
        CancellationToken cancellationToken) => workspaces.ListCurrentMembersAsync(cancellationToken);
}
