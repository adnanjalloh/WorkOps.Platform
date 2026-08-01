using WorkOps.Contracts.Common;

namespace WorkOps.Contracts.Features;

public sealed record UpdateWorkspacePlanRequest(
    [property: SanitizeAs(SanitizationProfile.Identifier)] string Plan);
