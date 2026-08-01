namespace WorkOps.Contracts.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    Guid SourceMessageId,
    string Channel,
    string Template,
    string EntityType,
    Guid EntityId,
    DateTimeOffset CreatedAt);
