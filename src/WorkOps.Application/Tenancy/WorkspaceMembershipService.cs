using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Common.Validation;
using WorkOps.Application.Identity;
using WorkOps.Domain.Identity;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Application.Tenancy;

public sealed class WorkspaceMembershipService(
    IdentityService identityService,
    IWorkspaceStore workspaces,
    IUnitOfWork unitOfWork,
    AuditWriter auditWriter,
    IWorkspaceContextAccessor workspaceContext,
    IInputSanitizer sanitizer,
    TimeProvider timeProvider)
{
    public async Task<WorkspaceMemberView> InviteAsync(
        string subject,
        string displayName,
        string role,
        CancellationToken cancellationToken)
    {
        var current = workspaceContext.Current
            ?? throw new InvalidOperationException("Workspace context is required.");
        if (!OidcSubject.IsValid(subject))
        {
            throw new RequestValidationException("invalid_identity_subject");
        }

        var safeDisplayName = sanitizer.Apply(
            displayName,
            InputProfile.PlainText,
            "body.displayName");
        var safeRole = sanitizer.Apply(role, InputProfile.Identifier, "body.role");
        if (!Enum.TryParse<WorkspaceRole>(safeRole, true, out var parsedRole) ||
            parsedRole is not WorkspaceRole.ProjectContributor and not WorkspaceRole.Viewer)
        {
            throw new RequestValidationException("invalid_membership_role");
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                var now = timeProvider.GetUtcNow();
                var user = await identityService.GetOrCreateAsync(
                    new CurrentIdentity(subject, safeDisplayName),
                    transactionCancellationToken);
                if (await workspaces.FindCurrentMembershipAsync(
                        user.Id,
                        transactionCancellationToken) is not null)
                {
                    throw new DuplicateWorkspaceMembershipException();
                }

                var membership = WorkspaceMembership.Create(
                    current.WorkspaceId,
                    user.Id,
                    parsedRole,
                    now);
                workspaces.Add(membership);
                auditWriter.Record(
                    AuditActions.MemberInvited,
                    "workspace_member",
                    user.Id,
                    now,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["role"] = parsedRole.ToString(),
                    });
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                return new WorkspaceMemberView(
                    user.Id,
                    user.DisplayName,
                    membership.Role,
                    membership.IsActive);
            },
            cancellationToken);
    }
}
