using WorkOps.Api.Tenancy;
using WorkOps.Application.Common.Validation;
using WorkOps.Application.Projects;
using WorkOps.Contracts.Common;
using WorkOps.Contracts.Projects;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Endpoints;

internal static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects")
            .RequireAuthorization()
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Header));

        group.MapPost("/", CreateAsync)
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithName("CreateProject");
        group.MapGet("/", ListAsync)
            .RequireAuthorization(Permissions.ProjectsRead)
            .WithName("ListProjects");
        group.MapGet("/{projectId:guid}", GetAsync)
            .RequireAuthorization(Permissions.ProjectsRead)
            .WithName("GetProject");
        group.MapPost("/{projectId:guid}/archive", ArchiveAsync)
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithName("ArchiveProject");

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateProjectRequest request,
        ProjectService projectService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var hasIdempotencyKey = httpContext.Request.Headers.TryGetValue(
            "Idempotency-Key",
            out var idempotencyValues);
        if (hasIdempotencyKey && idempotencyValues.Count != 1)
        {
            throw new RequestValidationException("invalid_idempotency_key");
        }

        if (!hasIdempotencyKey)
        {
            var project = await projectService.CreateAsync(
                request.Name,
                request.Key,
                cancellationToken);
            return Results.Created($"/api/v1/projects/{project.Id:D}", ToResponse(project));
        }

        var result = await projectService.CreateIdempotentAsync(
            request.Name,
            request.Key,
            idempotencyValues[0]!,
            cancellationToken);
        if (result.Replayed)
        {
            httpContext.Response.Headers["Idempotency-Replayed"] = "true";
        }

        return Results.Created(
            $"/api/v1/projects/{result.Project.Id:D}",
            ToResponse(result.Project));
    }

    private static async Task<IResult> ListAsync(
        ProjectService projectService,
        CancellationToken cancellationToken,
        [SkipSanitization(Reason = "The query value is parsed as an integer and range validated before use.")]
        int page = 1,
        [SkipSanitization(Reason = "The query value is parsed as an integer and range validated before use.")]
        int pageSize = 20,
        [SanitizeAs(SanitizationProfile.SearchText)] string? search = null,
        [SanitizeAs(SanitizationProfile.Identifier)] string? status = null)
    {
        var result = await projectService.ListAsync(
            page,
            pageSize,
            search,
            status,
            cancellationToken);
        return Results.Ok(new PagedResponse<ProjectResponse>(
            result.Items.Select(ToResponse).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid projectId,
        ProjectService projectService,
        CancellationToken cancellationToken)
    {
        var project = await projectService.GetAsync(projectId, cancellationToken);
        return project is null ? Results.NotFound() : Results.Ok(ToResponse(project));
    }

    private static async Task<IResult> ArchiveAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid projectId,
        ProjectService projectService,
        CancellationToken cancellationToken)
    {
        var found = await projectService.ArchiveAsync(projectId, cancellationToken);
        return found ? Results.NoContent() : Results.NotFound();
    }

    private static ProjectResponse ToResponse(ProjectView project) => new(
        project.Id,
        project.Name,
        project.Key,
        project.Status.ToString(),
        project.WorkItemCount,
        project.CreatedAt,
        project.UpdatedAt);
}
