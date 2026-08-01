using WorkOps.Domain;

namespace WorkOps.Application.Tenancy;

public interface IWorkspaceContextAccessor
{
    WorkspaceContext? Current { get; }

    WorkspaceId? CurrentWorkspaceId { get; }

    WorkspaceId? ProvisioningWorkspaceId { get; }

    void Establish(WorkspaceContext context);

    void EstablishBackground(WorkspaceId workspaceId);

    IDisposable BeginProvisioning(WorkspaceId workspaceId);
}
