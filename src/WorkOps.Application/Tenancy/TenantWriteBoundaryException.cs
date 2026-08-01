namespace WorkOps.Application.Tenancy;

public sealed class TenantWriteBoundaryException : InvalidOperationException
{
    public TenantWriteBoundaryException()
        : base("A tenant-scoped write was attempted outside its workspace boundary.")
    {
    }
}
