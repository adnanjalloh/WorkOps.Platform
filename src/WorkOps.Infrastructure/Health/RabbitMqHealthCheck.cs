using Microsoft.Extensions.Diagnostics.HealthChecks;
using WorkOps.Infrastructure.Messaging;

namespace WorkOps.Infrastructure.Health;

internal sealed class RabbitMqHealthCheck(RabbitMqSettings settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        _ = context;
        try
        {
            await using var connection = await RabbitMqConnectionFactory
                .Create(settings)
                .CreateConnectionAsync("workops-readiness", cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The message transport is unavailable.");
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("The message transport is unavailable.");
        }
    }
}
