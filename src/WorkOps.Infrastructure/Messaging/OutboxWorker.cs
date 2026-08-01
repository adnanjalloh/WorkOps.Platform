using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkOps.Application.Messaging;

namespace WorkOps.Infrastructure.Messaging;

internal sealed class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> LogResult =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2001, "OutboxResult"),
            "Outbox processing completed with {Result}");
    private static readonly Action<ILogger, Exception?> LogWorkerError =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2003, "OutboxWorkerError"),
            "Outbox processing encountered a transient infrastructure error");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                var result = await processor.ProcessNextAsync(stoppingToken);
                MessagingMetrics.RecordOutbox(result);
                if (result != OutboxProcessResult.NoMessage)
                {
                    LogResult(logger, result.ToString(), null);
                    continue;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                LogWorkerError(logger, null);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
