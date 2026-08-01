namespace WorkOps.Api.Tenancy;

internal sealed record WorkspaceContextRequirement(WorkspaceContextSource Source);

internal enum WorkspaceContextSource
{
    Route,
    Header,
}
