using WorkOps.Domain.Common;

namespace WorkOps.Domain.WorkItems;

public sealed class WorkItem : IWorkspaceOwned
{
    private WorkItem()
    {
    }

    private WorkItem(
        Guid id,
        WorkspaceId workspaceId,
        Guid projectId,
        string title,
        WorkItemPriority priority,
        Guid? assigneeUserId,
        string[] labels,
        DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        Title = title;
        Priority = priority;
        AssigneeUserId = assigneeUserId;
        Labels = labels;
        Status = WorkItemStatus.Backlog;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public WorkspaceId WorkspaceId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public WorkItemStatus Status { get; private set; }

    public WorkItemPriority Priority { get; private set; }

    public Guid? AssigneeUserId { get; private set; }

    public string[] Labels { get; private set; } = [];

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static WorkItem Create(
        WorkspaceId workspaceId,
        Guid projectId,
        string title,
        WorkItemPriority priority,
        Guid? assigneeUserId,
        IReadOnlyCollection<string> labels,
        DateTimeOffset createdAt) => new(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            title,
            priority,
            assigneeUserId,
            [.. labels],
            createdAt);

    public void UpdateDetails(
        string title,
        WorkItemPriority priority,
        Guid? assigneeUserId,
        IReadOnlyCollection<string> labels,
        DateTimeOffset updatedAt)
    {
        Title = title;
        Priority = priority;
        AssigneeUserId = assigneeUserId;
        Labels = [.. labels];
        UpdatedAt = updatedAt;
    }

    public void TransitionTo(WorkItemStatus target, DateTimeOffset updatedAt)
    {
        if (!CanTransition(Status, target))
        {
            throw new InvalidWorkItemTransitionException();
        }

        Status = target;
        UpdatedAt = updatedAt;
    }

    private static bool CanTransition(WorkItemStatus current, WorkItemStatus target) =>
        (current, target) switch
        {
            (WorkItemStatus.Backlog, WorkItemStatus.InProgress) => true,
            (WorkItemStatus.InProgress, WorkItemStatus.Blocked) => true,
            (WorkItemStatus.InProgress, WorkItemStatus.Completed) => true,
            (WorkItemStatus.Blocked, WorkItemStatus.InProgress) => true,
            _ => false,
        };
}
