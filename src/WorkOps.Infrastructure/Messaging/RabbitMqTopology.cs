using RabbitMQ.Client;

namespace WorkOps.Infrastructure.Messaging;

internal static class RabbitMqTopology
{
    public const string Exchange = "workops.events.v1";
    public const string DeadLetterExchange = "workops.failed.v1";
    public const string NotificationQueue = "workops.notifications.v1";
    public const string FailedNotificationQueue = "workops.notifications.failed.v1";
    public const string StatusChangedRoutingKey = "work-item.status-changed.v1";

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            Exchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            FailedNotificationQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            FailedNotificationQueue,
            DeadLetterExchange,
            StatusChangedRoutingKey,
            cancellationToken: cancellationToken);

        var queueArguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = StatusChangedRoutingKey,
        };
        await channel.QueueDeclareAsync(
            NotificationQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            NotificationQueue,
            Exchange,
            StatusChangedRoutingKey,
            cancellationToken: cancellationToken);
    }
}
