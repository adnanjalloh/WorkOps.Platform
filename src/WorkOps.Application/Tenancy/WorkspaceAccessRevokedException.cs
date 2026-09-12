namespace WorkOps.Application.Tenancy;

public sealed class WorkspaceAccessRevokedException : Exception
{
    public WorkspaceAccessRevokedException() : base("Workspace access is unavailable.")
    {
    }
}
