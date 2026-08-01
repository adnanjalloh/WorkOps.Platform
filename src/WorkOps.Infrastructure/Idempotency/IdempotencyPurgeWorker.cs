using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Common;

namespace WorkOps.Infrastructure.Idempotency;

internal sealed class IdempotencyPurgeWorker(
    IServiceScopeFactory scopeFactory,
    IdempotencyPurgeSettings settings,
    TimeProvider timeProvider,
    ILogger<IdempotencyPurgeWorker> logger) : BackgroundService
{
    private static readonly Meter Meter = new("WorkOps.Idempotency", BuildVersion.Current);
    private static readonly Counter<long> PurgedRecords = Meter.CreateCounter<long>(
        "workops.idempotency.purged");
    private static readonly Counter<long> PurgeResults = Meter.CreateCounter<long>(
        "workops.idempotency.purge.results");
    private static readonly Action<ILogger, int, Exception?> LogPurgeCompleted =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(2201, "IdempotencyPurgeCompleted"),
            "Idempotency retention purged {RecordCount} expired records");
    private static readonly Action<ILogger, Exception?> LogPurgeFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2202, "IdempotencyPurgeFailed"),
            "Idempotency retention encountered an infrastructure error");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(settings.Interval, timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            await PurgeAsync(stoppingToken);
            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                return;
            }
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var total = 0;
            for (var batch = 0; batch < settings.MaximumBatchesPerRun; batch++)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IIdempotencyMaintenanceStore>();
                var purged = await store.PurgeExpiredBatchAsync(
                    timeProvider.GetUtcNow(),
                    settings.BatchSize,
                    cancellationToken);
                total += purged;
                if (purged < settings.BatchSize)
                {
                    break;
                }
            }

            PurgedRecords.Add(total);
            PurgeResults.Add(1, new KeyValuePair<string, object?>("result", "completed"));
            if (total > 0)
            {
                LogPurgeCompleted(logger, total, null);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            PurgeResults.Add(1, new KeyValuePair<string, object?>("result", "failed"));
            LogPurgeFailed(logger, exception);
        }
    }
}
