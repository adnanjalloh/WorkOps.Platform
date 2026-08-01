using WorkOps.Application.Abstractions;

namespace WorkOps.Application.Messaging;

public sealed class DisabledMessagePublisher : IMessagePublisher
{
    public Task PublishAsync(OutboxLease message, CancellationToken cancellationToken)
    {
        _ = message;
        return Task.FromException(
            new InvalidOperationException("The message transport is disabled for this host."));
    }
}
