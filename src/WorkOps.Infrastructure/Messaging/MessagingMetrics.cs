using System.Diagnostics.Metrics;
using WorkOps.Application.Messaging;

namespace WorkOps.Infrastructure.Messaging;

internal static class MessagingMetrics
{
    private static readonly Meter Meter = new("WorkOps.Messaging", "1.0.0");
    private static readonly Counter<long> OutboxResults = Meter.CreateCounter<long>(
        "workops.outbox.results");
    private static readonly Counter<long> NotificationResults = Meter.CreateCounter<long>(
        "workops.notifications.results");
    private static readonly Histogram<double> OutboxDuration = Meter.CreateHistogram<double>(
        "workops.outbox.processing.duration",
        unit: "ms");
    private static long _outboxBacklog;

    static MessagingMetrics()
    {
        Meter.CreateObservableGauge(
            "workops.outbox.backlog",
            () => Interlocked.Read(ref _outboxBacklog),
            unit: "{message}");
    }

    public static void RecordOutbox(OutboxProcessResult result, TimeSpan duration)
    {
        var resultTag = new KeyValuePair<string, object?>("result", result.ToString());
        OutboxResults.Add(1, resultTag);
        OutboxDuration.Record(duration.TotalMilliseconds, resultTag);
    }

    public static void RecordNotification(string result) => NotificationResults.Add(
        1,
        new KeyValuePair<string, object?>("result", result));

    public static void SetOutboxBacklog(long count) => Interlocked.Exchange(
        ref _outboxBacklog,
        count);
}
