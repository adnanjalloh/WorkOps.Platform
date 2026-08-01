using WorkOps.Domain.Common;

namespace WorkOps.Domain.Features;

public sealed class WorkspaceSubscription : IWorkspaceOwned
{
    private WorkspaceSubscription()
    {
    }

    private WorkspaceSubscription(
        WorkspaceId workspaceId,
        WorkspacePlan plan,
        DateTimeOffset createdAt)
    {
        WorkspaceId = workspaceId;
        Plan = plan;
        CreatedAt = createdAt;
    }

    public WorkspaceId WorkspaceId { get; private set; }

    public WorkspacePlan Plan { get; private set; }

    public int ActiveProjectCount { get; private set; }

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static WorkspaceSubscription CreateStarter(
        WorkspaceId workspaceId,
        DateTimeOffset createdAt) => new(workspaceId, WorkspacePlan.Starter, createdAt);

    public void ReserveProjectSlot(int maximumActiveProjects, DateTimeOffset updatedAt)
    {
        if (ActiveProjectCount >= maximumActiveProjects)
        {
            throw new FeatureLimitExceededException();
        }

        ActiveProjectCount++;
        UpdatedAt = updatedAt;
    }

    public void ReleaseProjectSlot(DateTimeOffset updatedAt)
    {
        if (ActiveProjectCount > 0)
        {
            ActiveProjectCount--;
            UpdatedAt = updatedAt;
        }
    }

    public bool ChangePlan(WorkspacePlan plan, DateTimeOffset updatedAt)
    {
        if (Plan == plan)
        {
            return false;
        }

        Plan = plan;
        UpdatedAt = updatedAt;
        return true;
    }
}
