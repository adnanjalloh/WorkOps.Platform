namespace WorkOps.Domain.Tenancy;

public sealed class InactiveWorkspaceMembershipException : Exception
{
    public InactiveWorkspaceMembershipException() : base("Inactive membership cannot change role.")
    {
    }
}
