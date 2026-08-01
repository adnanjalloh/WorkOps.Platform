using WorkOps.Domain.WorkItems;

namespace WorkOps.Application.WorkItems;

public sealed record WorkItemView(
    Guid Id,
    Guid ProjectId,
    string Title,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    Guid? AssigneeUserId,
    string? AssigneeDisplayName,
    IReadOnlyList<string> Labels,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
