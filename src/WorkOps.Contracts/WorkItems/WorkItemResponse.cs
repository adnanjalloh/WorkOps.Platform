namespace WorkOps.Contracts.WorkItems;

public sealed record WorkItemResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Status,
    string Priority,
    Guid? AssigneeUserId,
    string? AssigneeDisplayName,
    IReadOnlyList<string> Labels,
    string Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
