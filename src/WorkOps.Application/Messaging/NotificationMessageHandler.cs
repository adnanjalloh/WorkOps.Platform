using WorkOps.Application.Abstractions;
using WorkOps.Application.Common.Validation;
using WorkOps.Application.Tenancy;
using WorkOps.Domain;

namespace WorkOps.Application.Messaging;

public sealed class NotificationMessageHandler(
    IWorkspaceContextAccessor workspaceContext,
    INotificationStore notifications,
    TimeProvider timeProvider)
{
    public async Task<bool> HandleAsync(
        string messageType,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                messageType,
                WorkItemStatusChangedMessage.MessageType,
                StringComparison.Ordinal))
        {
            throw new RequestValidationException("unsupported_message_type");
        }

        var message = MessagePayload.DeserializeWorkItemStatusChanged(payloadJson);
        workspaceContext.EstablishBackground(WorkspaceId.From(message.WorkspaceId));
        return await notifications.TryDeliverAsync(
            message,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
