namespace WorkOps.Application.Tenancy;

public sealed class MembershipManagementForbiddenException : Exception
{
    public MembershipManagementForbiddenException() : base("Membership management is not permitted.")
    {
    }
}
