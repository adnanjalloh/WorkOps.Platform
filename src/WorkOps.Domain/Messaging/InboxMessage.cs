using WorkOps.Domain.Common;

namespace WorkOps.Domain.Messaging;

public sealed class InboxMessage : IWorkspaceOwned
{
    private InboxMessage()
    {
    }

    private InboxMessage(
        WorkspaceId workspaceId,
        Guid messageId,
        string consumer,
        DateTimeOffset processedAt)
    {
        WorkspaceId = workspaceId;
        MessageId = messageId;
        Consumer = consumer;
        ProcessedAt = processedAt;
    }

    public WorkspaceId WorkspaceId { get; private set; }

    public Guid MessageId { get; private set; }

    public string Consumer { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; private set; }

    public static InboxMessage Record(
        WorkspaceId workspaceId,
        Guid messageId,
        string consumer,
        DateTimeOffset processedAt) => new(workspaceId, messageId, consumer, processedAt);
}
