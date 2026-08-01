using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WorkOps.Application.Common.Validation;
using WorkOps.Application.Messaging;

namespace WorkOps.Infrastructure.Messaging;

internal sealed class RabbitMqNotificationConsumer(
    RabbitMqSettings settings,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqNotificationConsumer> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> LogState =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2002, "NotificationConsumerState"),
            "Notification consumer state changed to {State}");
    private static readonly Action<ILogger, Exception?> LogConnectionError =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2005, "NotificationConsumerConnectionError"),
            "Notification consumer connection failed; retrying");
    private static readonly Action<ILogger, string, string, Exception?> LogMessageRejected =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2006, "NotificationMessageRejected"),
            "Notification message {MessageId} of type {MessageType} was rejected");
    private static readonly Action<ILogger, string, string, string, Exception?> LogHandlerError =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(2007, "NotificationHandlerError"),
            "Notification message {MessageId} of type {MessageType} failed with {Result}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogConnectionError(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await RabbitMqConnectionFactory
            .Create(settings)
            .CreateConnectionAsync("workops-notification-consumer", cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await RabbitMqTopology.DeclareAsync(channel, cancellationToken);
        await channel.BasicQosAsync(0, 1, false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var payloadJson = System.Text.Encoding.UTF8.GetString(delivery.Body.Span);
            var messageType = delivery.BasicProperties.Type ?? string.Empty;
            var safeMessageId = Guid.TryParse(delivery.BasicProperties.MessageId, out var messageId)
                ? messageId.ToString("D")
                : "invalid";
            var safeMessageType = string.Equals(
                messageType,
                WorkItemStatusChangedMessage.MessageType,
                StringComparison.Ordinal)
                ? WorkItemStatusChangedMessage.MessageType
                : "unsupported";
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<NotificationMessageHandler>();
                var delivered = await handler.HandleAsync(
                    messageType,
                    payloadJson,
                    cancellationToken);
                MessagingMetrics.RecordNotification(delivered ? "delivered" : "duplicate");
                await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
            }
            catch (RequestValidationException exception)
            {
                MessagingMetrics.RecordNotification("invalid");
                LogMessageRejected(logger, safeMessageId, safeMessageType, exception);
                await channel.BasicNackAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                var requeue = !delivery.Redelivered;
                var result = requeue ? "retry" : "failed";
                MessagingMetrics.RecordNotification(result);
                LogHandlerError(logger, safeMessageId, safeMessageType, result, exception);
                await channel.BasicNackAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    requeue,
                    cancellationToken);
            }
        };

        await channel.BasicConsumeAsync(
            RabbitMqTopology.NotificationQueue,
            autoAck: false,
            consumer,
            cancellationToken);
        LogState(logger, "ready", null);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
