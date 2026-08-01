using WorkOps.Application.Abstractions;
using WorkOps.Domain;

namespace WorkOps.Application.Features;

public sealed class UncachedFeatureCache : IFeatureCache
{
    public Task<FeatureSnapshot> GetOrCreateAsync(
        WorkspaceId workspaceId,
        Func<CancellationToken, Task<FeatureSnapshot>> factory,
        CancellationToken cancellationToken)
    {
        _ = workspaceId;
        return factory(cancellationToken);
    }

    public Task InvalidateAsync(
        WorkspaceId workspaceId,
        CancellationToken cancellationToken)
    {
        _ = workspaceId;
        return Task.CompletedTask;
    }
}
