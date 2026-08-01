using WorkOps.Domain.Common;

namespace WorkOps.Domain.Notifications;

public sealed class NotificationDelivery : IWorkspaceOwned
{
    private NotificationDelivery()
    {
    }

    private NotificationDelivery(
        Guid id,
        WorkspaceId workspaceId,
        Guid sourceMessageId,
        Guid recipientUserId,
        string channel,
        string template,
        string entityType,
        Guid entityId,
        DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        SourceMessageId = sourceMessageId;
        RecipientUserId = recipientUserId;
        Channel = channel;
        Template = template;
        EntityType = entityType;
        EntityId = entityId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public WorkspaceId WorkspaceId { get; private set; }

    public Guid SourceMessageId { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public string Channel { get; private set; } = string.Empty;

    public string Template { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static NotificationDelivery Create(
        WorkspaceId workspaceId,
        Guid sourceMessageId,
        Guid recipientUserId,
        string channel,
        string template,
        string entityType,
        Guid entityId,
        DateTimeOffset createdAt) => new(
            Guid.NewGuid(),
            workspaceId,
            sourceMessageId,
            recipientUserId,
            channel,
            template,
            entityType,
            entityId,
            createdAt);
}
