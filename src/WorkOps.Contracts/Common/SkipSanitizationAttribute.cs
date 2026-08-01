namespace WorkOps.Contracts.Common;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SkipSanitizationAttribute : Attribute
{
    public required string Reason { get; init; }
}
