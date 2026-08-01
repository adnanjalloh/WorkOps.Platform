namespace WorkOps.Application.Messaging;

public static class OutboxRetryPolicy
{
    public const int MaximumAttempts = 5;

    public static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);

    public static TimeSpan GetDelay(Guid messageId, int attemptCount)
    {
        var boundedAttempt = Math.Clamp(attemptCount, 1, MaximumAttempts);
        var baseSeconds = Math.Min(Math.Pow(2, boundedAttempt - 1), 60);
        var bytes = messageId.ToByteArray();
        var jitterMilliseconds = BitConverter.ToUInt16(bytes, 0) % 1000;
        return TimeSpan.FromSeconds(baseSeconds) + TimeSpan.FromMilliseconds(jitterMilliseconds);
    }
}
