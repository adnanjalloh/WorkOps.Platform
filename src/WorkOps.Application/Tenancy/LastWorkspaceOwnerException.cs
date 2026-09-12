namespace WorkOps.Application.Tenancy;

public sealed class LastWorkspaceOwnerException : Exception
{
    public LastWorkspaceOwnerException() : base("The last active workspace owner must be retained.")
    {
    }
}
