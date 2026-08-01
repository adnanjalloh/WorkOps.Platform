using Microsoft.AspNetCore.Diagnostics;
using WorkOps.Application.Common;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Common.Validation;
using WorkOps.Application.Files;
using WorkOps.Application.Idempotency;
using WorkOps.Application.Projects;
using WorkOps.Application.Tenancy;
using WorkOps.Application.WorkItems;
using WorkOps.Domain.Features;
using WorkOps.Domain.Messaging;
using WorkOps.Domain.WorkItems;

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

            case DuplicateProjectKeyException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "Project key is unavailable",
                    "project_key_conflict",
                    cancellationToken);
                return true;

            case DuplicateWorkspaceMembershipException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "Workspace membership already exists",
                    "workspace_membership_conflict",
                    cancellationToken);
                return true;

            case ProjectArchivedException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "Project is archived",
                    "project_archived",
                    cancellationToken);
                return true;

            case ConcurrencyConflictException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "The resource changed since it was read",
                    "concurrency_conflict",
                    cancellationToken);
                return true;

            case InvalidWorkItemTransitionException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Work item transition is not allowed",
                    "invalid_work_item_transition",
                    cancellationToken);
                return true;

            case InvalidAssigneeException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Assignee is not an active workspace member",
                    "invalid_assignee",
                    cancellationToken);
                return true;

            case OutboxReplayNotAllowedException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "Outbox message cannot be replayed",
                    "outbox_replay_not_allowed",
                    cancellationToken);
                return true;

            case FeatureLimitExceededException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "Workspace plan limit reached",
                    "feature_limit_exceeded",
                    cancellationToken);
                return true;

            case AttachmentRejectedException rejectedAttachment:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Attachment was rejected",
                    rejectedAttachment.Code,
                    cancellationToken);
                return true;

            case FileScannerUnavailableException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status503ServiceUnavailable,
                    "Attachment scanning is unavailable",
                    "attachment_scanner_unavailable",
                    cancellationToken);
                return true;

            case AttachmentContentUnavailableException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status503ServiceUnavailable,
                    "Attachment content is unavailable",
                    "attachment_content_unavailable",
                    cancellationToken);
                return true;

            case IdempotencyKeyConflictException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "Idempotency key was reused with different input",
                    "idempotency_key_conflict",
                    cancellationToken);
                return true;

            case IdempotencyRaceException:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "An identical request is still being committed",
                    "idempotency_request_in_progress",
                    cancellationToken);
                return true;

            case RequestValidationException validation:
                await WriteProblemAsync(
                    httpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Submitted input is invalid",
                    validation.Code,
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
        _ = cancellationToken;
        return Results.Problem(
                statusCode: statusCode,
                title: title,
                extensions: new Dictionary<string, object?> { ["code"] = code })
            .ExecuteAsync(httpContext);
    }
}
