using WorkOps.Application.Abstractions;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Common.Validation;
using WorkOps.Domain.Identity;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Application.Tenancy;

public sealed class WorkspaceMembershipService(
    IUserStore users,
    IWorkspaceStore workspaces,
    IUnitOfWork unitOfWork,
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
        var safeSubject = sanitizer.Apply(subject, InputProfile.Identifier, "body.subject");
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

        var now = timeProvider.GetUtcNow();
        var user = await users.FindBySubjectAsync(safeSubject, cancellationToken);
        if (user is null)
        {
            user = ApplicationUser.Create(safeSubject, safeDisplayName, now);
            users.Add(user);
        }
        else
        {
            user.UpdateDisplayName(safeDisplayName, now);
        }

        if (await workspaces.FindCurrentMembershipAsync(user.Id, cancellationToken) is not null)
        {
            throw new DuplicateWorkspaceMembershipException();
        }

        var membership = WorkspaceMembership.Create(
            current.WorkspaceId,
            user.Id,
            parsedRole,
            now);
        workspaces.Add(membership);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new WorkspaceMemberView(user.Id, user.DisplayName, membership.Role, membership.IsActive);
    }
}
