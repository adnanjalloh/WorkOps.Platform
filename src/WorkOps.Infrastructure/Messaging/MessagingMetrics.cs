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

    public static void RecordOutbox(OutboxProcessResult result) => OutboxResults.Add(
        1,
        new KeyValuePair<string, object?>("result", result.ToString()));

    public static void RecordNotification(string result) => NotificationResults.Add(
        1,
        new KeyValuePair<string, object?>("result", result));
}
