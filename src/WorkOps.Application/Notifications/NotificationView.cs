namespace WorkOps.Application.Notifications;

public sealed record NotificationView(
    Guid Id,
    Guid SourceMessageId,
    string Channel,
    string Template,
    string EntityType,
    Guid EntityId,
    DateTimeOffset CreatedAt);
