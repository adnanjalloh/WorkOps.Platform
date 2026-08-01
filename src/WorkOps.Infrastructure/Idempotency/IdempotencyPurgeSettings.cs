namespace WorkOps.Infrastructure.Idempotency;

internal sealed record IdempotencyPurgeSettings(
    TimeSpan Interval,
    int BatchSize,
    int MaximumBatchesPerRun);
