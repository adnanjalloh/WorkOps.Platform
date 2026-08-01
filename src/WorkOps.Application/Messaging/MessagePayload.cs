using System.Text.Json;
using WorkOps.Application.Common.Validation;
using WorkOps.Domain.WorkItems;

namespace WorkOps.Application.Messaging;

public static class MessagePayload
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(WorkItemStatusChangedMessage message) =>
        JsonSerializer.Serialize(message, SerializerOptions);

    public static WorkItemStatusChangedMessage DeserializeWorkItemStatusChanged(string payloadJson)
    {
        WorkItemStatusChangedMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<WorkItemStatusChangedMessage>(
                payloadJson,
                SerializerOptions);
        }
        catch (JsonException)
        {
            throw new RequestValidationException("invalid_message_payload");
        }

        if (message is null ||
            message.MessageId == Guid.Empty ||
            message.WorkspaceId == Guid.Empty ||
            message.ActorUserId == Guid.Empty ||
            message.RecipientUserId == Guid.Empty ||
            message.WorkItemId == Guid.Empty ||
            string.IsNullOrWhiteSpace(message.CorrelationId) ||
            message.CorrelationId.Length > 128 ||
            message.CorrelationId.Contains('\r') ||
            message.CorrelationId.Contains('\n') ||
            !IsStatus(message.PreviousStatus) ||
            !IsStatus(message.CurrentStatus))
        {
            throw new RequestValidationException("invalid_message_payload");
        }

        return message;
    }

    private static bool IsStatus(string value) =>
        Enum.TryParse<WorkItemStatus>(value, true, out var status) && Enum.IsDefined(status);
}
