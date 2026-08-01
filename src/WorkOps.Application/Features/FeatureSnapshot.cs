namespace WorkOps.Application.Features;

public sealed record FeatureSnapshot(
    string Plan,
    int MaximumActiveProjects,
    int ActiveProjectCount);
