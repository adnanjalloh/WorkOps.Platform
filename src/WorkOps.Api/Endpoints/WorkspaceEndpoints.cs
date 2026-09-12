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
        group.MapPost("/{workspaceId:guid}/invitations", InviteMemberAsync)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Route))
            .RequireAuthorization(Permissions.MembersManage)
            .WithName("InviteWorkspaceMember");
        group.MapMethods("/{workspaceId:guid}/members/{userId:guid}/role", [HttpMethods.Patch], ChangeMemberRoleAsync)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Route))
            .RequireAuthorization(Permissions.MembersManage)
            .WithName("ChangeWorkspaceMemberRole");
        group.MapPost("/{workspaceId:guid}/members/{userId:guid}/deactivation", DeactivateMemberAsync)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Route))
            .RequireAuthorization(Permissions.MembersManage)
            .WithName("DeactivateWorkspaceMember");

        return endpoints;
    }

    private static async Task<IResult> InviteMemberAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid workspaceId,
        InviteWorkspaceMemberRequest request,
        WorkspaceMembershipService membershipService,
        CancellationToken cancellationToken)
    {
        var member = await membershipService.InviteAsync(
            request.Subject,
            request.DisplayName,
            request.Role,
            cancellationToken);
        return Results.Created(
            $"/api/v1/workspaces/{workspaceId:D}/members",
            ToMemberResponse(member));
    }

    private static async Task<IResult> ChangeMemberRoleAsync(
        [SkipSanitization(Reason = "The route workspace is parsed as a non-empty Guid and authorized by workspace middleware.")]
        Guid workspaceId,
        [SkipSanitization(Reason = "The route member ID is parsed as a Guid and rejects an empty Guid in the service.")]
        Guid userId,
        ChangeWorkspaceMemberRoleRequest request,
        WorkspaceMembershipService membershipService,
        CancellationToken cancellationToken)
    {
        _ = workspaceId;
        var member = await membershipService.ChangeRoleAsync(
            userId, request.Role, request.ExpectedVersion, cancellationToken);
        return member is null ? Results.NotFound() : Results.Ok(ToMemberResponse(member));
    }

    private static async Task<IResult> DeactivateMemberAsync(
        [SkipSanitization(Reason = "The route workspace is parsed as a non-empty Guid and authorized by workspace middleware.")]
        Guid workspaceId,
        [SkipSanitization(Reason = "The route member ID is parsed as a Guid and rejects an empty Guid in the service.")]
        Guid userId,
        DeactivateWorkspaceMemberRequest request,
        WorkspaceMembershipService membershipService,
        CancellationToken cancellationToken)
    {
        _ = workspaceId;
        var member = await membershipService.DeactivateAsync(userId, request.ExpectedVersion, cancellationToken);
        return member is null ? Results.NotFound() : Results.Ok(ToMemberResponse(member));
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
        return Results.Ok(members.Select(ToMemberResponse));
    }

    private static WorkspaceMemberResponse ToMemberResponse(WorkspaceMemberView member) => new(
        member.UserId, member.DisplayName, member.Role.ToString(), member.IsActive,
        MembershipVersion.Encode(member.Version));

    private static WorkspaceResponse ToResponse(WorkOps.Domain.Tenancy.Workspace workspace) => new(
        workspace.Id.Value,
        workspace.Name,
        workspace.Slug,
        workspace.Status.ToString(),
        workspace.CreatedAt);
}
