using WorkOps.Domain;

namespace WorkOps.Application.Tenancy;

public sealed class WorkspaceContextAccessor : IWorkspaceContextAccessor
{
    public WorkspaceContext? Current { get; private set; }

    public WorkspaceId? CurrentWorkspaceId => Current?.WorkspaceId;

    public void Establish(WorkspaceContext context)
    {
        if (Current is not null)
        {
            throw new InvalidOperationException("Workspace context has already been established.");
        }

        Current = context;
    }
}
