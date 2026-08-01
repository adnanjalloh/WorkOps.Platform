using System.Text;
using RabbitMQ.Client;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Messaging;

namespace WorkOps.Infrastructure.Messaging;

internal sealed class RabbitMqMessagePublisher(RabbitMqSettings settings)
    : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(OutboxLease message, CancellationToken cancellationToken)
    {
        if (!string.Equals(
                message.Type,
                WorkItemStatusChangedMessage.MessageType,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The outbox message type has no configured route.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.Id.ToString("D"),
                Type = message.Type,
                Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds()),
            };
            var body = Encoding.UTF8.GetBytes(message.PayloadJson);
            await _channel!.BasicPublishAsync(
                RabbitMqTopology.Exchange,
                RabbitMqTopology.StatusChangedRoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_connection is null || !_connection.IsOpen)
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            _connection = await RabbitMqConnectionFactory
                .Create(settings)
                .CreateConnectionAsync("workops-outbox-publisher", cancellationToken);
        }

        if (_channel is null || !_channel.IsOpen)
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
            }

            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true),
                cancellationToken);
            await RabbitMqTopology.DeclareAsync(_channel, cancellationToken);
        }
    }
}
