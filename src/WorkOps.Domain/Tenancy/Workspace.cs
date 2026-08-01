namespace WorkOps.Domain.Tenancy;

public sealed class Workspace
{
    private Workspace()
    {
    }

    private Workspace(WorkspaceId id, string name, string slug, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Status = WorkspaceStatus.Active;
        CreatedAt = createdAt;
    }

    public WorkspaceId Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public WorkspaceStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static Workspace Create(string name, string slug, DateTimeOffset createdAt) =>
        new(WorkspaceId.New(), name, slug, createdAt);

    public void Suspend(DateTimeOffset updatedAt)
    {
        Status = WorkspaceStatus.Suspended;
        UpdatedAt = updatedAt;
    }

    public void Activate(DateTimeOffset updatedAt)
    {
        Status = WorkspaceStatus.Active;
        UpdatedAt = updatedAt;
    }
}
