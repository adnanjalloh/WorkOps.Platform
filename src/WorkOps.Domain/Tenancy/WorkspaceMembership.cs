using WorkOps.Domain.Common;

namespace WorkOps.Domain.Tenancy;

public sealed class WorkspaceMembership : IWorkspaceOwned
{
    private WorkspaceMembership()
    {
    }

    private WorkspaceMembership(
        WorkspaceId workspaceId,
        Guid userId,
        WorkspaceRole role,
        DateTimeOffset createdAt)
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public WorkspaceId WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static WorkspaceMembership Create(
        WorkspaceId workspaceId,
        Guid userId,
        WorkspaceRole role,
        DateTimeOffset createdAt)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        return new(workspaceId, userId, role, createdAt);
    }

    public void ChangeRole(WorkspaceRole role, DateTimeOffset updatedAt)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        if (!IsActive)
        {
            throw new InactiveWorkspaceMembershipException();
        }

        if (Role != role)
        {
            Role = role;
            UpdatedAt = updatedAt;
        }
    }

    public void Deactivate(DateTimeOffset updatedAt)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = updatedAt;
    }
}
