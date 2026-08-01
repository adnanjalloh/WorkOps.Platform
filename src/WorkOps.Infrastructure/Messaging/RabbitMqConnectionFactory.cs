using RabbitMQ.Client;

namespace WorkOps.Infrastructure.Messaging;

internal static class RabbitMqConnectionFactory
{
    public static ConnectionFactory Create(RabbitMqSettings settings) => new()
    {
        HostName = settings.HostName,
        Port = settings.Port,
        VirtualHost = settings.VirtualHost,
        UserName = settings.UserName,
        Password = settings.Password,
        AutomaticRecoveryEnabled = true,
        TopologyRecoveryEnabled = true,
        RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
    };
}
