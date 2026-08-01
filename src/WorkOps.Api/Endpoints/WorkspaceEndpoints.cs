using WorkOps.Api.Authentication;
using WorkOps.Api.Tenancy;
using WorkOps.Application.Tenancy;
using WorkOps.Contracts.Common;
using WorkOps.Contracts.Tenancy;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Endpoints;

internal static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/workspaces").RequireAuthorization();

        group.MapPost("/", CreateWorkspaceAsync).WithName("CreateWorkspace");
        group.MapGet("/{workspaceId:guid}", GetWorkspaceAsync)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Route))
            .RequireAuthorization(Permissions.WorkspacesRead)
            .WithName("GetWorkspace");
        group.MapGet("/{workspaceId:guid}/members", ListMembersAsync)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Route))
            .RequireAuthorization(Permissions.MembersRead)
            .WithName("ListWorkspaceMembers");

        return endpoints;
    }

    private static async Task<IResult> CreateWorkspaceAsync(
        CreateWorkspaceRequest request,
        HttpContext httpContext,
        WorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var identity = CurrentIdentityFactory.Create(httpContext.User);
        var workspace = await workspaceService.CreateAsync(
            identity,
            request.Name,
            request.Slug,
            cancellationToken);
        var response = ToResponse(workspace);
        return Results.Created($"/api/v1/workspaces/{workspace.Id.Value:D}", response);
    }

    private static async Task<IResult> GetWorkspaceAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid workspaceId,
        WorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        _ = workspaceId;
        var workspace = await workspaceService.GetCurrentAsync(cancellationToken);
        return workspace is null ? Results.NotFound() : Results.Ok(ToResponse(workspace));
    }

    private static async Task<IResult> ListMembersAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid workspaceId,
        WorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        _ = workspaceId;
        var members = await workspaceService.ListCurrentMembersAsync(cancellationToken);
        return Results.Ok(members.Select(member => new WorkspaceMemberResponse(
            member.UserId,
            member.DisplayName,
            member.Role.ToString(),
            member.IsActive)));
    }

    private static WorkspaceResponse ToResponse(WorkOps.Domain.Tenancy.Workspace workspace) => new(
        workspace.Id.Value,
        workspace.Name,
        workspace.Slug,
        workspace.Status.ToString(),
        workspace.CreatedAt);
}
