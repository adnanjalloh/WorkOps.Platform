using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Features;
using WorkOps.Domain.Features;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Features;

internal sealed class WorkspaceSubscriptionStore(WorkOpsDbContext dbContext)
    : IWorkspaceSubscriptionStore
{
    public void Add(WorkspaceSubscription subscription) =>
        dbContext.WorkspaceSubscriptions.Add(subscription);

    public Task<WorkspaceSubscription?> FindCurrentAsync(CancellationToken cancellationToken) =>
        dbContext.WorkspaceSubscriptions.SingleOrDefaultAsync(cancellationToken);

    public Task<FeatureSnapshot?> GetCurrentSnapshotAsync(CancellationToken cancellationToken) =>
        dbContext.WorkspaceSubscriptions
            .AsNoTracking()
            .Select(subscription => new FeatureSnapshot(
                subscription.Plan.ToString(),
                subscription.Plan == WorkspacePlan.Starter ? 2 : 20,
                subscription.ActiveProjectCount))
            .SingleOrDefaultAsync(cancellationToken);
}
