namespace WorkOps.Application.Tenancy;

public sealed class TenantWriteBoundaryException : InvalidOperationException
{
    public TenantWriteBoundaryException()
        : base("A tenant-owned write was attempted outside its workspace boundary.")
    {
    }
}
