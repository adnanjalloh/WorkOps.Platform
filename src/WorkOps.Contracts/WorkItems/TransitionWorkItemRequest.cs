using WorkOps.Contracts.Common;

namespace WorkOps.Contracts.WorkItems;

public sealed record TransitionWorkItemRequest(
    [property: SanitizeAs(SanitizationProfile.Identifier)] string TargetStatus,
    [property: SanitizeAs(SanitizationProfile.Identifier)] string ExpectedVersion);
