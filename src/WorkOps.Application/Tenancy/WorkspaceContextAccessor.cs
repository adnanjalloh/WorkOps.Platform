using WorkOps.Domain;

namespace WorkOps.Application.Tenancy;

public sealed class WorkspaceContextAccessor : IWorkspaceContextAccessor
{
    private WorkspaceId? _backgroundWorkspaceId;
    private WorkspaceId? _provisioningWorkspaceId;

    public WorkspaceContext? Current { get; private set; }

    public WorkspaceId? CurrentWorkspaceId => Current?.WorkspaceId ?? _backgroundWorkspaceId;

    public WorkspaceId? ProvisioningWorkspaceId => _provisioningWorkspaceId;

    public void Establish(WorkspaceContext context)
    {
        if (Current is not null || _backgroundWorkspaceId.HasValue || _provisioningWorkspaceId.HasValue)
        {
            throw new InvalidOperationException("Workspace context has already been established.");
        }

        Current = context;
    }

    public void EstablishBackground(WorkspaceId workspaceId)
    {
        if (Current is not null || _backgroundWorkspaceId.HasValue || _provisioningWorkspaceId.HasValue)
        {
            throw new InvalidOperationException("Workspace context has already been established.");
        }

        _backgroundWorkspaceId = workspaceId;
    }

    public IDisposable BeginProvisioning(WorkspaceId workspaceId)
    {
        if (Current is not null || _backgroundWorkspaceId.HasValue || _provisioningWorkspaceId.HasValue)
        {
            throw new InvalidOperationException("Workspace context has already been established.");
        }

        _provisioningWorkspaceId = workspaceId;
        return new ProvisioningScope(this, workspaceId);
    }

    private void EndProvisioning(WorkspaceId workspaceId)
    {
        if (_provisioningWorkspaceId != workspaceId)
        {
            throw new InvalidOperationException("Workspace provisioning context does not match the active scope.");
        }

        _provisioningWorkspaceId = null;
    }

    private sealed class ProvisioningScope(
        WorkspaceContextAccessor accessor,
        WorkspaceId workspaceId) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            accessor.EndProvisioning(workspaceId);
            _disposed = true;
        }
    }
}
