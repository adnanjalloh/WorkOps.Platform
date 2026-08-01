using WorkOps.Application.Abstractions;

namespace WorkOps.Application.Messaging;

public sealed class OutboxProcessor(
    IOutboxStore outboxStore,
    IMessagePublisher publisher,
    TimeProvider timeProvider)
{
    public async Task<OutboxProcessResult> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var message = await outboxStore.LeaseNextAsync(
            now,
            now + OutboxRetryPolicy.LeaseDuration,
            cancellationToken);
        if (message is null)
        {
            return OutboxProcessResult.NoMessage;
        }

        try
        {
            await publisher.PublishAsync(message, cancellationToken);
            await outboxStore.MarkProcessedAsync(
                message.Id,
                timeProvider.GetUtcNow(),
                cancellationToken);
            return OutboxProcessResult.Published;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            var failedAt = timeProvider.GetUtcNow();
            var delay = OutboxRetryPolicy.GetDelay(message.Id, message.AttemptCount);
            await outboxStore.MarkPublishFailureAsync(
                message.Id,
                failedAt,
                failedAt + delay,
                OutboxRetryPolicy.MaximumAttempts,
                "transport_publish_failed",
                cancellationToken);
            return message.AttemptCount >= OutboxRetryPolicy.MaximumAttempts
                ? OutboxProcessResult.Failed
                : OutboxProcessResult.RetryScheduled;
        }
    }
}
