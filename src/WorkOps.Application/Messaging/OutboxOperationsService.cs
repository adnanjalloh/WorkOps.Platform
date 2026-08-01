using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;

namespace WorkOps.Application.Messaging;

public sealed class OutboxOperationsService(
    IOutboxStore outboxStore,
    IUnitOfWork unitOfWork,
    AuditWriter auditWriter,
    TimeProvider timeProvider)
{
    public async Task<bool> ReplayAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await outboxStore.FindCurrentAsync(messageId, cancellationToken);
        if (message is null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        message.Replay(now);
        auditWriter.Record(
            AuditActions.OutboxReplayed,
            "outbox_message",
            message.Id,
            now,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["messageType"] = message.Type,
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
