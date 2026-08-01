using WorkOps.Application.Features;
using WorkOps.Domain.Features;

namespace WorkOps.Application.Abstractions;

public interface IWorkspaceSubscriptionStore
{
    void Add(WorkspaceSubscription subscription);

    Task<WorkspaceSubscription?> FindCurrentAsync(CancellationToken cancellationToken);

    Task<FeatureSnapshot?> GetCurrentSnapshotAsync(CancellationToken cancellationToken);
}
