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

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static WorkspaceMembership Create(
        WorkspaceId workspaceId,
        Guid userId,
        WorkspaceRole role,
        DateTimeOffset createdAt) => new(workspaceId, userId, role, createdAt);

    public void Deactivate(DateTimeOffset updatedAt)
    {
        IsActive = false;
        UpdatedAt = updatedAt;
    }
}
