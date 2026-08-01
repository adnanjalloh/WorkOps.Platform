using WorkOps.Domain;

namespace WorkOps.Application.Tenancy;

public sealed class WorkspaceContextAccessor : IWorkspaceContextAccessor
{
    private WorkspaceId? _backgroundWorkspaceId;

    public WorkspaceContext? Current { get; private set; }

    public WorkspaceId? CurrentWorkspaceId => Current?.WorkspaceId ?? _backgroundWorkspaceId;

    public void Establish(WorkspaceContext context)
    {
        if (Current is not null || _backgroundWorkspaceId.HasValue)
        {
            throw new InvalidOperationException("Workspace context has already been established.");
        }

        Current = context;
    }

    public void EstablishBackground(WorkspaceId workspaceId)
    {
        if (Current is not null || _backgroundWorkspaceId.HasValue)
        {
            throw new InvalidOperationException("Workspace context has already been established.");
        }

        _backgroundWorkspaceId = workspaceId;
    }
}
