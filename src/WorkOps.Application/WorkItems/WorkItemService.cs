using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Common.Validation;
using WorkOps.Application.Messaging;
using WorkOps.Application.Projects;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Messaging;
using WorkOps.Domain.Projects;
using WorkOps.Domain.WorkItems;

namespace WorkOps.Application.WorkItems;

public sealed class WorkItemService(
    IProjectStore projects,
    IWorkItemStore workItems,
    IWorkspaceStore workspaces,
    IOutboxStore outbox,
    IUnitOfWork unitOfWork,
    AuditWriter auditWriter,
    IWorkspaceContextAccessor workspaceContext,
    ICorrelationContext correlationContext,
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
        auditWriter.Record(
            AuditActions.WorkItemCreated,
            "work_item",
            workItem.Id,
            workItem.CreatedAt,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["priority"] = workItem.Priority.ToString(),
                ["status"] = workItem.Status.ToString(),
            });
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

        var changedFields = GetChangedFields(
            workItem,
            safeTitle,
            parsedPriority,
            assigneeUserId,
            safeLabels);
        var now = timeProvider.GetUtcNow();

        workItem.UpdateDetails(
            safeTitle,
            parsedPriority,
            assigneeUserId,
            safeLabels,
            now);
        auditWriter.Record(
            AuditActions.WorkItemUpdated,
            "work_item",
            workItem.Id,
            now,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["fields"] = changedFields.Length == 0
                    ? "none"
                    : string.Join(',', changedFields),
            });
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

        var current = workspaceContext.Current
            ?? throw new InvalidOperationException("An interactive workspace context is required.");
        var previousStatus = workItem.Status;
        var now = timeProvider.GetUtcNow();
        workItem.TransitionTo(parsedTarget, now);
        auditWriter.Record(
            AuditActions.WorkItemTransitioned,
            "work_item",
            workItem.Id,
            now,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["currentStatus"] = parsedTarget.ToString(),
                ["previousStatus"] = previousStatus.ToString(),
            });

        var messageId = Guid.NewGuid();
        var message = new WorkItemStatusChangedMessage(
            messageId,
            current.WorkspaceId.Value,
            current.UserId,
            workItem.AssigneeUserId ?? current.UserId,
            workItem.Id,
            previousStatus.ToString(),
            parsedTarget.ToString(),
            now,
            correlationContext.CorrelationId);
        outbox.Add(OutboxMessage.Create(
            messageId,
            current.WorkspaceId,
            WorkItemStatusChangedMessage.MessageType,
            MessagePayload.Serialize(message),
            now));
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

    private static string[] GetChangedFields(
        WorkItem workItem,
        string title,
        WorkItemPriority priority,
        Guid? assigneeUserId,
        IReadOnlyList<string> labels)
    {
        var changedFields = new List<string>();
        if (!string.Equals(workItem.Title, title, StringComparison.Ordinal))
        {
            changedFields.Add("title");
        }

        if (workItem.Priority != priority)
        {
            changedFields.Add("priority");
        }

        if (workItem.AssigneeUserId != assigneeUserId)
        {
            changedFields.Add("assignee");
        }

        if (!workItem.Labels.SequenceEqual(labels, StringComparer.Ordinal))
        {
            changedFields.Add("labels");
        }

        return [.. changedFields];
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
