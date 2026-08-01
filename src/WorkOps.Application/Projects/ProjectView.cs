using WorkOps.Domain.Projects;

namespace WorkOps.Application.Projects;

public sealed record ProjectView(
    Guid Id,
    string Name,
    string Key,
    ProjectStatus Status,
    int WorkItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
