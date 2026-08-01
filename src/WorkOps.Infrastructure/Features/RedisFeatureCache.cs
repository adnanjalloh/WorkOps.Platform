using System.Diagnostics.Metrics;
using System.Text.Json;
using StackExchange.Redis;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Features;
using WorkOps.Domain;

namespace WorkOps.Infrastructure.Features;

internal sealed class RedisFeatureCache(IConnectionMultiplexer connection) : IFeatureCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Meter Meter = new("WorkOps.Cache", "1.0.0");
    private static readonly Counter<long> CacheResults = Meter.CreateCounter<long>("workops.cache.results");

    public async Task<FeatureSnapshot> GetOrCreateAsync(
        WorkspaceId workspaceId,
        Func<CancellationToken, Task<FeatureSnapshot>> factory,
        CancellationToken cancellationToken)
    {
        var database = connection.GetDatabase();
        var cacheKey = CacheKey(workspaceId);
        try
        {
            var cached = await database.StringGetAsync(cacheKey);
            if (cached.HasValue)
            {
                CacheResults.Add(1, new KeyValuePair<string, object?>("result", "hit"));
                return Deserialize(cached!);
            }

            CacheResults.Add(1, new KeyValuePair<string, object?>("result", "miss"));
            var lockKey = $"{cacheKey}:lock";
            var lockValue = Guid.NewGuid().ToString("N");
            if (await database.LockTakeAsync(lockKey, lockValue, TimeSpan.FromSeconds(5)))
            {
                try
                {
                    cached = await database.StringGetAsync(cacheKey);
                    if (cached.HasValue)
                    {
                        return Deserialize(cached!);
                    }

                    var created = await factory(cancellationToken);
                    await database.StringSetAsync(
                        cacheKey,
                        JsonSerializer.Serialize(created, SerializerOptions),
                        TimeSpan.FromMinutes(5));
                    return created;
                }
                finally
                {
                    await database.LockReleaseAsync(lockKey, lockValue);
                }
            }

            for (var attempt = 0; attempt < 5; attempt++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                cached = await database.StringGetAsync(cacheKey);
                if (cached.HasValue)
                {
                    return Deserialize(cached!);
                }
            }
        }
        catch (RedisException)
        {
            CacheResults.Add(1, new KeyValuePair<string, object?>("result", "unavailable"));
        }
        catch (JsonException)
        {
            CacheResults.Add(1, new KeyValuePair<string, object?>("result", "corrupt"));
            try
            {
                await database.KeyDeleteAsync(cacheKey);
            }
            catch (RedisException)
            {
                CacheResults.Add(1, new KeyValuePair<string, object?>("result", "unavailable"));
            }
        }

        return await factory(cancellationToken);
    }

    public async Task InvalidateAsync(
        WorkspaceId workspaceId,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            await connection.GetDatabase().KeyDeleteAsync(CacheKey(workspaceId));
        }
        catch (RedisException)
        {
            CacheResults.Add(1, new KeyValuePair<string, object?>("result", "unavailable"));
        }
    }

    private static string CacheKey(WorkspaceId workspaceId) =>
        $"workops:{workspaceId.Value:N}:features";

    private static FeatureSnapshot Deserialize(RedisValue value)
    {
        var snapshot = JsonSerializer.Deserialize<FeatureSnapshot>((string)value!, SerializerOptions);
        if (snapshot is null ||
            string.IsNullOrWhiteSpace(snapshot.Plan) ||
            snapshot.MaximumActiveProjects <= 0 ||
            snapshot.ActiveProjectCount < 0 ||
            snapshot.ActiveProjectCount > snapshot.MaximumActiveProjects)
        {
            throw new JsonException("Cached feature data does not match the expected schema.");
        }

        return snapshot;
    }
}
