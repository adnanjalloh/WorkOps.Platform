namespace WorkOps.Application.Messaging;

public sealed record WorkItemStatusChangedMessage(
    Guid MessageId,
    Guid WorkspaceId,
    Guid ActorUserId,
    Guid RecipientUserId,
    Guid WorkItemId,
    string PreviousStatus,
    string CurrentStatus,
    DateTimeOffset OccurredAt,
    string CorrelationId)
{
    public const string MessageType = "work-item.status-changed.v1";
}
