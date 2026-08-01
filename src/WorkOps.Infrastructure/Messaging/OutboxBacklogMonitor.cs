using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkOps.Application.Abstractions;

namespace WorkOps.Infrastructure.Messaging;

internal sealed class OutboxBacklogMonitor(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxBacklogMonitor> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogMonitorError =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2004, "OutboxBacklogMonitorError"),
            "Outbox backlog observation encountered a transient infrastructure error");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
                MessagingMetrics.SetOutboxBacklog(await store.CountBacklogAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogMonitorError(logger, exception);
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                return;
            }
        }
    }
}
