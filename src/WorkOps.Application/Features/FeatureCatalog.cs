using WorkOps.Domain.Features;

namespace WorkOps.Application.Features;

public static class FeatureCatalog
{
    public static int MaximumActiveProjects(WorkspacePlan plan) => plan switch
    {
        WorkspacePlan.Starter => 2,
        WorkspacePlan.Team => 20,
        _ => throw new ArgumentOutOfRangeException(nameof(plan)),
    };
}
