using Microsoft.Extensions.Logging;
using WorkOps.Application.Abstractions;

namespace WorkOps.Application.Messaging;

public sealed class OutboxProcessor(
    IOutboxStore outboxStore,
    IMessagePublisher publisher,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    private static readonly Action<ILogger, Guid, string, string, string, Exception?> LogPublishFailure =
        LoggerMessage.Define<Guid, string, string, string>(
            LogLevel.Warning,
            new EventId(2004, "OutboxPublishFailed"),
            "Outbox message {MessageId} of type {MessageType} produced {Result} after {FailureCategory}");

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
        catch (Exception exception)
        {
            var failedAt = timeProvider.GetUtcNow();
            var delay = OutboxRetryPolicy.GetDelay(message.Id, message.AttemptCount);
            var result = message.AttemptCount >= OutboxRetryPolicy.MaximumAttempts
                ? OutboxProcessResult.Failed
                : OutboxProcessResult.RetryScheduled;
            var safeMessageType = string.Equals(
                message.Type,
                WorkItemStatusChangedMessage.MessageType,
                StringComparison.Ordinal)
                ? WorkItemStatusChangedMessage.MessageType
                : "unknown";
            LogPublishFailure(
                logger,
                message.Id,
                safeMessageType,
                result.ToString(),
                ClassifyFailure(exception),
                null);
            await outboxStore.MarkPublishFailureAsync(
                message.Id,
                failedAt,
                failedAt + delay,
                OutboxRetryPolicy.MaximumAttempts,
                "transport_publish_failed",
                cancellationToken);
            return result;
        }
    }

    private static string ClassifyFailure(Exception exception) => exception switch
    {
        TimeoutException => "timeout",
        InvalidOperationException => "invalid_operation",
        _ => "transport_error",
    };
}
