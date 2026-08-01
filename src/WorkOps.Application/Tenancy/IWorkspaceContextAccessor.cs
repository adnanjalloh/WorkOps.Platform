using WorkOps.Domain;

namespace WorkOps.Application.Tenancy;

public interface IWorkspaceContextAccessor
{
    WorkspaceContext? Current { get; }

    WorkspaceId? CurrentWorkspaceId { get; }

    void Establish(WorkspaceContext context);
}
