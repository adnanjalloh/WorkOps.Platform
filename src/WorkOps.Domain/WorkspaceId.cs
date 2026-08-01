namespace WorkOps.Domain;

public readonly record struct WorkspaceId
{
    private WorkspaceId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static WorkspaceId New() => new(Guid.NewGuid());

    public static WorkspaceId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("A workspace identifier cannot be empty.", nameof(value))
        : new WorkspaceId(value);

    public override string ToString() => Value.ToString("D");
}
