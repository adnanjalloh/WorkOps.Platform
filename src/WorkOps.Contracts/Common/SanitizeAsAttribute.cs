namespace WorkOps.Contracts.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SanitizeAsAttribute(SanitizationProfile profile) : Attribute
{
    public SanitizationProfile Profile { get; } = profile;
}
