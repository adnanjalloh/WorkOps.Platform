using WorkOps.Application.Common.Validation;
using WorkOps.Application.Messaging;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class MessagePayloadTests
{
    [TestMethod]
    public void Status_changed_message_round_trips_without_user_content()
    {
        var message = new WorkItemStatusChangedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Backlog",
            "InProgress",
            new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
            "correlation-123");

        var json = MessagePayload.Serialize(message);
        var result = MessagePayload.DeserializeWorkItemStatusChanged(json);

        Assert.AreEqual(message, result);
        Assert.DoesNotContain("title", json, StringComparison.OrdinalIgnoreCase);
    }

    [DataRow("{}")]
    [DataRow("not-json")]
    [TestMethod]
    public void Invalid_internal_message_is_rejected(string payload)
    {
        var exception = Assert.ThrowsExactly<RequestValidationException>(
            () => MessagePayload.DeserializeWorkItemStatusChanged(payload));

        Assert.AreEqual("invalid_message_payload", exception.Code);
    }
}
