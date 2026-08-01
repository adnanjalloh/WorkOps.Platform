using System.Security.Claims;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Tenancy;
using WorkOps.Domain;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Tenancy;

internal sealed class WorkspaceContextMiddleware(RequestDelegate next)
{
    private const string WorkspaceHeader = "X-Workspace-Id";

    public async Task InvokeAsync(
        HttpContext httpContext,
        WorkspaceAccessService accessService,
        IWorkspaceContextAccessor workspaceContext,
        IInputSanitizer sanitizer)
    {
        var requirement = httpContext.GetEndpoint()?.Metadata.GetMetadata<WorkspaceContextRequirement>();
        if (requirement is null || httpContext.User.Identity?.IsAuthenticated != true)
        {
            await next(httpContext);
            return;
        }

        var workspaceId = ResolveWorkspaceId(httpContext, requirement.Source, sanitizer);
        if (workspaceId is null)
        {
            await WriteProblemAsync(
                httpContext,
                StatusCodes.Status400BadRequest,
                "Invalid workspace context",
                "invalid_workspace_context");
            return;
        }

        var subject = httpContext.User.FindFirstValue("sub");
        if (subject is null)
        {
            await next(httpContext);
            return;
        }

        var access = await accessService.FindAsync(subject, workspaceId.Value, httpContext.RequestAborted);
        if (access is null)
        {
            await WriteProblemAsync(
                httpContext,
                StatusCodes.Status404NotFound,
                "Resource not found",
                "workspace_not_found");
            return;
        }

        if (access.Status == WorkspaceStatus.Suspended)
        {
            await WriteProblemAsync(
                httpContext,
                StatusCodes.Status403Forbidden,
                "Workspace is suspended",
                "workspace_suspended");
            return;
        }

        workspaceContext.Establish(new WorkspaceContext(
            access.UserId,
            access.WorkspaceId,
            access.Role,
            access.Status));

        await next(httpContext);
    }

    private static WorkspaceId? ResolveWorkspaceId(
        HttpContext httpContext,
        WorkspaceContextSource source,
        IInputSanitizer sanitizer)
    {
        var submitted = source switch
        {
            WorkspaceContextSource.Route => httpContext.Request.RouteValues["workspaceId"]?.ToString(),
            WorkspaceContextSource.Header when httpContext.Request.Headers.TryGetValue(
                WorkspaceHeader,
                out var values) && values.Count == 1 => values[0],
            _ => null,
        };

        if (submitted is null)
        {
            return null;
        }

        try
        {
            var safeValue = sanitizer.Apply(
                submitted,
                InputProfile.HeaderValue,
                source == WorkspaceContextSource.Route ? "route.workspaceId" : "header.X-Workspace-Id");
            return Guid.TryParse(safeValue, out var parsed) && parsed != Guid.Empty
                ? WorkspaceId.From(parsed)
                : null;
        }
        catch (InputRejectedException)
        {
            return null;
        }
    }

    private static Task WriteProblemAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string code) => Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code })
        .ExecuteAsync(httpContext);
}
