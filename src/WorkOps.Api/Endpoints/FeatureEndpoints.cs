using WorkOps.Api.Tenancy;
using WorkOps.Application.Features;
using WorkOps.Contracts.Common;
using WorkOps.Contracts.Features;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Endpoints;

internal static class FeatureEndpoints
{
    public static IEndpointRouteBuilder MapFeatureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/features", GetAsync)
            .RequireAuthorization(Permissions.WorkspacesRead)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Header))
            .WithName("GetFeatureEntitlements");
        endpoints.MapPut("/api/v1/workspaces/{workspaceId:guid}/plan", UpdatePlanAsync)
            .RequireAuthorization(Permissions.OperationsManage)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Route))
            .WithName("UpdateWorkspacePlan");
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        FeatureService featureService,
        CancellationToken cancellationToken)
    {
        var snapshot = await featureService.GetCurrentAsync(cancellationToken);
        return Results.Ok(ToResponse(snapshot));
    }

    private static async Task<IResult> UpdatePlanAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid workspaceId,
        UpdateWorkspacePlanRequest request,
        FeatureService featureService,
        CancellationToken cancellationToken)
    {
        _ = workspaceId;
        var found = await featureService.UpdatePlanAsync(request.Plan, cancellationToken);
        if (!found)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToResponse(await featureService.GetCurrentAsync(cancellationToken)));
    }

    private static FeatureEntitlementsResponse ToResponse(FeatureSnapshot snapshot) => new(
        snapshot.Plan,
        snapshot.MaximumActiveProjects,
        snapshot.ActiveProjectCount);
}
