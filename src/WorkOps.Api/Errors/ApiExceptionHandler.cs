using Microsoft.AspNetCore.Diagnostics;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Tenancy;

namespace WorkOps.Api.Errors;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, string, int, Exception?> LogInputRejected =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Warning,
            new EventId(1001, "InputRejected"),
            "Input rejected at {Path} using {Profile}; submitted length {Length}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case InputRejectedException rejected:
                LogInputRejected(
                    logger,
                    rejected.Path,
                    rejected.Profile.ToString(),
                    rejected.SubmittedLength,
                    null);
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Submitted input is invalid",
                    "input_rejected",
                    cancellationToken);
                return true;

            case DuplicateWorkspaceSlugException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "Workspace slug is unavailable",
                    "workspace_slug_conflict",
                    cancellationToken);
                return true;

            default:
                return false;
        }
    }

    private static Task WriteProblemAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string code,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = statusCode;
        return httpContext.Response.WriteAsJsonAsync(
            new
            {
                type = "about:blank",
                title,
                status = statusCode,
                code,
            },
            cancellationToken);
    }
}
