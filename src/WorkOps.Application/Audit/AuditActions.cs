namespace WorkOps.Application.Audit;

public static class AuditActions
{
    public const string WorkspaceCreated = "workspace.created";
    public const string MemberInvited = "member.invited";
    public const string ProjectCreated = "project.created";
    public const string ProjectArchived = "project.archived";
    public const string WorkItemCreated = "work_item.created";
    public const string WorkItemUpdated = "work_item.updated";
    public const string WorkItemTransitioned = "work_item.transitioned";
    public const string OutboxReplayed = "outbox.replayed";
    public const string WorkspacePlanChanged = "workspace.plan_changed";
    public const string AttachmentUploaded = "attachment.uploaded";
}
