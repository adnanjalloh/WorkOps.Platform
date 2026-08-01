namespace WorkOps.Contracts.Features;

public sealed record FeatureEntitlementsResponse(
    string Plan,
    int MaximumActiveProjects,
    int ActiveProjectCount);
