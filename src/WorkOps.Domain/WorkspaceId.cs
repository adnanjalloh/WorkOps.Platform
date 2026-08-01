namespace WorkOps.Domain;

public readonly record struct WorkspaceId
{
    private WorkspaceId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static WorkspaceId New() => new(Guid.NewGuid());
}
