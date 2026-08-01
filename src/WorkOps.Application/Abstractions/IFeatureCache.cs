using WorkOps.Application.Features;
using WorkOps.Domain;

namespace WorkOps.Application.Abstractions;

public interface IFeatureCache
{
    Task<FeatureSnapshot> GetOrCreateAsync(
        WorkspaceId workspaceId,
        Func<CancellationToken, Task<FeatureSnapshot>> factory,
        CancellationToken cancellationToken);

    Task InvalidateAsync(WorkspaceId workspaceId, CancellationToken cancellationToken);
}
