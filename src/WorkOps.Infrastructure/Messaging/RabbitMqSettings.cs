namespace WorkOps.Infrastructure.Messaging;

internal sealed record RabbitMqSettings(
    string HostName,
    int Port,
    string VirtualHost,
    string UserName,
    string Password);
