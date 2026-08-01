using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace WorkOps.Infrastructure.Health;

internal sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        _ = context;
        try
        {
            _ = cancellationToken;
            await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (RedisException)
        {
            return HealthCheckResult.Unhealthy("The cache is unavailable.");
        }
    }
}
