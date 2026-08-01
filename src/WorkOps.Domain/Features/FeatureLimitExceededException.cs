namespace WorkOps.Domain.Features;

public sealed class FeatureLimitExceededException : Exception
{
    public FeatureLimitExceededException()
        : base("The workspace plan limit has been reached.")
    {
    }
}
