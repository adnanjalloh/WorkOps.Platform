namespace WorkOps.Contracts.Tenancy;

public sealed record WorkspaceResponse(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    DateTimeOffset CreatedAt);
