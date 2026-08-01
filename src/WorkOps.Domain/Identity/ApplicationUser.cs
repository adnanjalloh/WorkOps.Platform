namespace WorkOps.Domain.Identity;

public sealed class ApplicationUser
{
    private ApplicationUser()
    {
    }

    private ApplicationUser(Guid id, string subject, string displayName, DateTimeOffset createdAt)
    {
        Id = id;
        Subject = subject;
        DisplayName = displayName;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static ApplicationUser Create(string subject, string displayName, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), subject, displayName, createdAt);

    public void UpdateDisplayName(string displayName, DateTimeOffset updatedAt)
    {
        if (string.Equals(DisplayName, displayName, StringComparison.Ordinal))
        {
            return;
        }

        DisplayName = displayName;
        UpdatedAt = updatedAt;
    }
}
