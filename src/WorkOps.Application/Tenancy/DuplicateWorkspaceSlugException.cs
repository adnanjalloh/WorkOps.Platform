namespace WorkOps.Application.Tenancy;

public sealed class DuplicateWorkspaceSlugException : Exception
{
    public DuplicateWorkspaceSlugException()
        : base("A workspace with the submitted slug already exists.")
    {
    }
}
