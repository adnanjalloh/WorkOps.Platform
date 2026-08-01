using WorkOps.Domain.Common;

namespace WorkOps.Domain.Audit;

public sealed class AuditEvent : IWorkspaceOwned
{
    private AuditEvent()
    {
    }

    private AuditEvent(
        Guid id,
        WorkspaceId workspaceId,
        Guid actorUserId,
        string action,
        string entityType,
        Guid entityId,
        DateTimeOffset occurredAt,
        string correlationId,
        string metadataJson)
    {
        Id = id;
        WorkspaceId = workspaceId;
        ActorUserId = actorUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
        MetadataJson = metadataJson;
    }

    public Guid Id { get; private set; }

    public WorkspaceId WorkspaceId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public string MetadataJson { get; private set; } = string.Empty;

    public static AuditEvent Record(
        WorkspaceId workspaceId,
        Guid actorUserId,
        string action,
        string entityType,
        Guid entityId,
        DateTimeOffset occurredAt,
        string correlationId,
        string metadataJson) => new(
            Guid.NewGuid(),
            workspaceId,
            actorUserId,
            action,
            entityType,
            entityId,
            occurredAt,
            correlationId,
            metadataJson);
}
