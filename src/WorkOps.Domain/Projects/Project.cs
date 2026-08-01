using WorkOps.Domain.Common;

namespace WorkOps.Domain.Projects;

public sealed class Project : IWorkspaceOwned
{
    private Project()
    {
    }

    private Project(
        Guid id,
        WorkspaceId workspaceId,
        string name,
        string key,
        DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Name = name;
        Key = key;
        Status = ProjectStatus.Active;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public WorkspaceId WorkspaceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    public ProjectStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static Project Create(
        WorkspaceId workspaceId,
        string name,
        string key,
        DateTimeOffset createdAt) => new(Guid.NewGuid(), workspaceId, name, key, createdAt);

    public bool Archive(DateTimeOffset updatedAt)
    {
        if (Status == ProjectStatus.Archived)
        {
            return false;
        }

        Status = ProjectStatus.Archived;
        UpdatedAt = updatedAt;
        return true;
    }
}
