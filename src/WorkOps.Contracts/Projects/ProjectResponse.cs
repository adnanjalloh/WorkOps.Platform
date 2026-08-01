namespace WorkOps.Contracts.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string Key,
    string Status,
    int WorkItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
