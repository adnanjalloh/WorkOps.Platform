namespace WorkOps.Application.Tenancy;

public interface IWorkspaceContextAccessor
{
    WorkspaceContext? Current { get; }

    void Establish(WorkspaceContext context);
}
