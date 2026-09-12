using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common;
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
        var parsedRole = ParseRole(role);
        if (parsedRole is not WorkspaceRole.ProjectContributor and not WorkspaceRole.Viewer)
        {
            throw new RequestValidationException("invalid_membership_role");
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                await LockAndAuthorizeAsync(transactionCancellationToken);
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
                    membership.IsActive,
                    membership.Version);
            },
            cancellationToken);
    }

    public Task<WorkspaceMemberView?> ChangeRoleAsync(
        Guid userId,
        string role,
        string expectedVersion,
        CancellationToken cancellationToken) =>
        MutateAsync(userId, ParseRole(role), expectedVersion, cancellationToken);

    public Task<WorkspaceMemberView?> DeactivateAsync(
        Guid userId,
        string expectedVersion,
        CancellationToken cancellationToken) =>
        MutateAsync(userId, null, expectedVersion, cancellationToken);

    private Task<WorkspaceMemberView?> MutateAsync(
        Guid userId,
        WorkspaceRole? newRole,
        string expectedVersion,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new RequestValidationException("invalid_member_id");
        }

        if (!MembershipVersion.TryDecode(expectedVersion, out var version))
        {
            throw new RequestValidationException("invalid_membership_version");
        }

        return unitOfWork.ExecuteInTransactionAsync<WorkspaceMemberView?>(
            async transactionCancellationToken =>
            {
                var actor = await LockAndAuthorizeAsync(transactionCancellationToken);
                var membership = await workspaces.FindCurrentMembershipAsync(userId, transactionCancellationToken);
                if (membership is null)
                {
                    return null;
                }

                if (actor.Role != WorkspaceRole.Owner &&
                    (membership.Role is not WorkspaceRole.ProjectContributor and not WorkspaceRole.Viewer ||
                     newRole is not null and not WorkspaceRole.ProjectContributor and not WorkspaceRole.Viewer))
                {
                    throw new MembershipManagementForbiddenException();
                }

                if (membership.Version != version)
                {
                    throw new ConcurrencyConflictException();
                }

                if (newRole.HasValue && !membership.IsActive)
                {
                    throw new InactiveWorkspaceMembershipException();
                }

                if ((!newRole.HasValue && !membership.IsActive) ||
                    (newRole.HasValue && membership.Role == newRole.Value))
                {
                    return await workspaces.GetCurrentMemberAsync(userId, transactionCancellationToken);
                }

                if (membership.Role == WorkspaceRole.Owner &&
                    newRole != WorkspaceRole.Owner &&
                    !await workspaces.HasOtherActiveOwnerAsync(userId, transactionCancellationToken))
                {
                    throw new LastWorkspaceOwnerException();
                }

                var previousRole = membership.Role;
                var now = timeProvider.GetUtcNow();
                if (newRole.HasValue)
                {
                    membership.ChangeRole(newRole.Value, now);
                    auditWriter.Record(
                        AuditActions.MemberRoleChanged,
                        "workspace_member",
                        userId,
                        now,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["previousRole"] = previousRole.ToString(),
                            ["currentRole"] = newRole.Value.ToString(),
                        });
                }
                else
                {
                    membership.Deactivate(now);
                    auditWriter.Record(
                        AuditActions.MemberDeactivated,
                        "workspace_member",
                        userId,
                        now,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["role"] = previousRole.ToString(),
                        });
                }

                await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                return await workspaces.GetCurrentMemberAsync(userId, transactionCancellationToken);
            },
            cancellationToken);
    }

    private async Task<WorkspaceMemberView> LockAndAuthorizeAsync(CancellationToken cancellationToken)
    {
        var current = workspaceContext.Current
            ?? throw new InvalidOperationException("An interactive workspace context is required.");
        var workspace = await workspaces.LockCurrentForMembershipChangeAsync(cancellationToken);
        // The middleware's role snapshot can become stale while this request waits for the lock.
        var actor = await workspaces.GetCurrentMemberAsync(current.UserId, cancellationToken);
        if (workspace is null || actor is not { IsActive: true })
        {
            throw new WorkspaceAccessRevokedException();
        }

        if (workspace.Status != WorkspaceStatus.Active ||
            actor.Role is not WorkspaceRole.Owner and not WorkspaceRole.Administrator)
        {
            throw new MembershipManagementForbiddenException();
        }

        return actor;
    }

    private WorkspaceRole ParseRole(string role)
    {
        var safeRole = sanitizer.Apply(role, InputProfile.Identifier, "body.role");
        if (!Enum.TryParse<WorkspaceRole>(safeRole, true, out var parsedRole) ||
            !Enum.IsDefined(parsedRole) ||
            !string.Equals(parsedRole.ToString(), safeRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("invalid_membership_role");
        }

        return parsedRole;
    }
}
