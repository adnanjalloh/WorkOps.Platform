using WorkOps.Application.Messaging;

namespace WorkOps.Application.Abstractions;

public interface IMessagePublisher
{
    Task PublishAsync(OutboxLease message, CancellationToken cancellationToken);
}
