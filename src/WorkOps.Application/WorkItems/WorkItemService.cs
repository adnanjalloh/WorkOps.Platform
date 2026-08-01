using WorkOps.Application.Abstractions;
using WorkOps.Application.Common;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Common.Validation;
using WorkOps.Application.Projects;
using WorkOps.Domain.Projects;
using WorkOps.Domain.WorkItems;

namespace WorkOps.Application.WorkItems;

public sealed class WorkItemService(
    IProjectStore projects,
    IWorkItemStore workItems,
    IWorkspaceStore workspaces,
    IUnitOfWork unitOfWork,
    IInputSanitizer sanitizer,
    TimeProvider timeProvider)
{
    public async Task<WorkItemView?> CreateAsync(
        Guid projectId,
        string title,
        string priority,
        Guid? assigneeUserId,
        IReadOnlyList<string>? labels,
        CancellationToken cancellationToken)
    {
        var project = await projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        if (project.Status == ProjectStatus.Archived)
        {
            throw new ProjectArchivedException();
        }

        var safeTitle = sanitizer.Apply(title, InputProfile.PlainText, "body.title");
        var parsedPriority = ParsePriority(priority);
        var safeLabels = NormalizeLabels(labels);
        await EnsureValidAssigneeAsync(assigneeUserId, cancellationToken);

        var workItem = WorkItem.Create(
            project.WorkspaceId,
            project.Id,
            safeTitle,
            parsedPriority,
            assigneeUserId,
            safeLabels,
            timeProvider.GetUtcNow());
        workItems.Add(workItem);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await workItems.GetAsync(workItem.Id, cancellationToken)
            ?? throw new InvalidOperationException("Created work item could not be read.");
    }

    public Task<WorkItemView?> GetAsync(Guid workItemId, CancellationToken cancellationToken) =>
        workItems.GetAsync(workItemId, cancellationToken);

    public async Task<WorkItemView?> UpdateAsync(
        Guid workItemId,
        string title,
        string priority,
        Guid? assigneeUserId,
        IReadOnlyList<string>? labels,
        string expectedVersion,
        CancellationToken cancellationToken)
    {
        var workItem = await workItems.FindAsync(workItemId, cancellationToken);
        if (workItem is null)
        {
            return null;
        }

        EnsureExpectedVersion(workItem, expectedVersion);
        var safeTitle = sanitizer.Apply(title, InputProfile.PlainText, "body.title");
        var parsedPriority = ParsePriority(priority);
        var safeLabels = NormalizeLabels(labels);
        await EnsureValidAssigneeAsync(assigneeUserId, cancellationToken);

        workItem.UpdateDetails(
            safeTitle,
            parsedPriority,
            assigneeUserId,
            safeLabels,
            timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await workItems.GetAsync(workItem.Id, cancellationToken);
    }

    public async Task<WorkItemView?> TransitionAsync(
        Guid workItemId,
        string targetStatus,
        string expectedVersion,
        CancellationToken cancellationToken)
    {
        var workItem = await workItems.FindAsync(workItemId, cancellationToken);
        if (workItem is null)
        {
            return null;
        }

        EnsureExpectedVersion(workItem, expectedVersion);
        var safeTarget = sanitizer.Apply(
            targetStatus,
            InputProfile.Identifier,
            "body.targetStatus");
        if (!Enum.TryParse<WorkItemStatus>(safeTarget, true, out var parsedTarget) ||
            !Enum.IsDefined(parsedTarget))
        {
            throw new RequestValidationException("invalid_work_item_status");
        }

        workItem.TransitionTo(parsedTarget, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await workItems.GetAsync(workItem.Id, cancellationToken);
    }

    private WorkItemPriority ParsePriority(string priority)
    {
        var safePriority = sanitizer.Apply(priority, InputProfile.Identifier, "body.priority");
        if (!Enum.TryParse<WorkItemPriority>(safePriority, true, out var parsedPriority) ||
            !Enum.IsDefined(parsedPriority))
        {
            throw new RequestValidationException("invalid_work_item_priority");
        }

        return parsedPriority;
    }

    private string[] NormalizeLabels(IReadOnlyList<string>? labels)
    {
        if (labels is null || labels.Count > 5)
        {
            throw new RequestValidationException("invalid_work_item_labels");
        }

        var safeLabels = labels
            .Select((label, index) => sanitizer.Apply(
                label,
                InputProfile.KeyPath,
                $"body.labels[{index}]"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (safeLabels.Distinct(StringComparer.Ordinal).Count() != safeLabels.Length)
        {
            throw new RequestValidationException("invalid_work_item_labels");
        }

        return safeLabels;
    }

    private async Task EnsureValidAssigneeAsync(
        Guid? assigneeUserId,
        CancellationToken cancellationToken)
    {
        if (assigneeUserId.HasValue &&
            !await workspaces.IsCurrentMemberActiveAsync(assigneeUserId.Value, cancellationToken))
        {
            throw new InvalidAssigneeException();
        }
    }

    private void EnsureExpectedVersion(WorkItem workItem, string expectedVersion)
    {
        var safeVersion = sanitizer.Apply(
            expectedVersion,
            InputProfile.Identifier,
            "body.expectedVersion");
        if (!WorkItemVersion.TryDecode(safeVersion, out var parsedVersion))
        {
            throw new RequestValidationException("invalid_concurrency_token");
        }

        if (parsedVersion != workItem.Version)
        {
            throw new ConcurrencyConflictException();
        }
    }
}
