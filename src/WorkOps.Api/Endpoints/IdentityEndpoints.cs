using WorkOps.Api.Authentication;
using WorkOps.Api.Tenancy;
using WorkOps.Application.Identity;
using WorkOps.Application.Tenancy;
using WorkOps.Contracts.Identity;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Endpoints;

internal static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/me").RequireAuthorization();

        group.MapGet("/", GetMeAsync).WithName("GetCurrentUser");
        group.MapGet("/capabilities", GetCapabilities)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Header))
            .RequireAuthorization(Permissions.WorkspacesRead)
            .WithName("GetCurrentUserCapabilities");

        return endpoints;
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext httpContext,
        IdentityService identityService,
        CancellationToken cancellationToken)
    {
        var identity = CurrentIdentityFactory.Create(httpContext.User);
        var result = await identityService.GetMeAsync(identity, cancellationToken);
        var memberships = result.Memberships
            .Select(membership => new MembershipResponse(
                membership.WorkspaceId.Value,
                membership.WorkspaceName,
                membership.WorkspaceSlug,
                membership.WorkspaceStatus.ToString(),
                membership.Role.ToString()))
            .ToArray();

        return Results.Ok(new MeResponse(result.User.Id, result.User.DisplayName, memberships));
    }

    private static IResult GetCapabilities(IWorkspaceContextAccessor workspaceContext)
    {
        var current = workspaceContext.Current
            ?? throw new InvalidOperationException("Workspace context is required.");
        var permissions = Permissions.ForRole(current.Role).Order(StringComparer.Ordinal).ToArray();

        return Results.Ok(new CapabilitiesResponse(
            current.WorkspaceId.Value,
            current.Role.ToString(),
            permissions));
    }
}
