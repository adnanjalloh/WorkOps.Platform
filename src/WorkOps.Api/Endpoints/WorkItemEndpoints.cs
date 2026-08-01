using WorkOps.Api.Tenancy;
using WorkOps.Application.WorkItems;
using WorkOps.Contracts.Common;
using WorkOps.Contracts.WorkItems;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Endpoints;

internal static class WorkItemEndpoints
{
    public static IEndpointRouteBuilder MapWorkItemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/projects/{projectId:guid}/work-items", CreateAsync)
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Header))
            .WithName("CreateWorkItem");

        var group = endpoints.MapGroup("/api/v1/work-items")
            .RequireAuthorization()
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Header));
        group.MapGet("/{workItemId:guid}", GetAsync)
            .RequireAuthorization(Permissions.ProjectsRead)
            .WithName("GetWorkItem");
        group.MapMethods("/{workItemId:guid}", [HttpMethods.Patch], UpdateAsync)
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithName("UpdateWorkItem");
        group.MapPost("/{workItemId:guid}/transitions", TransitionAsync)
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithName("TransitionWorkItem");

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid projectId,
        CreateWorkItemRequest request,
        WorkItemService workItemService,
        CancellationToken cancellationToken)
    {
        var workItem = await workItemService.CreateAsync(
            projectId,
            request.Title,
            request.Priority,
            request.AssigneeUserId,
            request.Labels,
            cancellationToken);
        return workItem is null
            ? Results.NotFound()
            : Results.Created($"/api/v1/work-items/{workItem.Id:D}", ToResponse(workItem));
    }

    private static async Task<IResult> GetAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid workItemId,
        WorkItemService workItemService,
        CancellationToken cancellationToken)
    {
        var workItem = await workItemService.GetAsync(workItemId, cancellationToken);
        return workItem is null ? Results.NotFound() : Results.Ok(ToResponse(workItem));
    }

    private static async Task<IResult> UpdateAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid workItemId,
        UpdateWorkItemRequest request,
        WorkItemService workItemService,
        CancellationToken cancellationToken)
    {
        var workItem = await workItemService.UpdateAsync(
            workItemId,
            request.Title,
            request.Priority,
            request.AssigneeUserId,
            request.Labels,
            request.ExpectedVersion,
            cancellationToken);
        return workItem is null ? Results.NotFound() : Results.Ok(ToResponse(workItem));
    }

    private static async Task<IResult> TransitionAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid workItemId,
        TransitionWorkItemRequest request,
        WorkItemService workItemService,
        CancellationToken cancellationToken)
    {
        var workItem = await workItemService.TransitionAsync(
            workItemId,
            request.TargetStatus,
            request.ExpectedVersion,
            cancellationToken);
        return workItem is null ? Results.NotFound() : Results.Ok(ToResponse(workItem));
    }

    private static WorkItemResponse ToResponse(WorkItemView workItem) => new(
        workItem.Id,
        workItem.ProjectId,
        workItem.Title,
        workItem.Status.ToString(),
        workItem.Priority.ToString(),
        workItem.AssigneeUserId,
        workItem.AssigneeDisplayName,
        workItem.Labels,
        WorkItemVersion.Encode(workItem.Version),
        workItem.CreatedAt,
        workItem.UpdatedAt);
}
