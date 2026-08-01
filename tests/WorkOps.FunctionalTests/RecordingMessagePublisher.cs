using WorkOps.Application.Abstractions;
using WorkOps.Application.Messaging;

namespace WorkOps.FunctionalTests;

internal sealed class RecordingMessagePublisher : IMessagePublisher
{
    private readonly List<OutboxLease> _messages = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<OutboxLease> Messages
    {
        get
        {
            lock (_lock)
            {
                return [.. _messages];
            }
        }
    }

    public Task PublishAsync(OutboxLease message, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _messages.Add(message);
        }

        return Task.CompletedTask;
    }
}
