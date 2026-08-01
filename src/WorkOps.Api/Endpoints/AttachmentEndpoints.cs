using WorkOps.Api.Tenancy;
using WorkOps.Application.Files;
using WorkOps.Contracts.Common;
using WorkOps.Contracts.Files;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Endpoints;

internal static class AttachmentEndpoints
{
    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/work-items/{workItemId:guid}/attachments", UploadAsync)
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Header))
            .DisableAntiforgery()
            .WithName("UploadAttachment");
        endpoints.MapGet("/api/v1/attachments/{attachmentId:guid}", DownloadAsync)
            .RequireAuthorization(Permissions.ProjectsRead)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Header))
            .WithName("DownloadAttachment");
        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid workItemId,
        IFormFile file,
        AttachmentService attachmentService,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var attachment = await attachmentService.UploadAsync(
            workItemId,
            file.FileName,
            file.ContentType,
            file.Length,
            content,
            cancellationToken);
        return attachment is null
            ? Results.NotFound()
            : Results.Created($"/api/v1/attachments/{attachment.Id:D}", ToResponse(attachment));
    }

    private static async Task<IResult> DownloadAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid attachmentId,
        HttpContext httpContext,
        AttachmentService attachmentService,
        CancellationToken cancellationToken)
    {
        var download = await attachmentService.DownloadAsync(attachmentId, cancellationToken);
        if (download is null)
        {
            return Results.NotFound();
        }

        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(
            download.Content,
            download.ContentType,
            download.FileName,
            enableRangeProcessing: false);
    }

    private static AttachmentResponse ToResponse(AttachmentView attachment) => new(
        attachment.Id,
        attachment.WorkItemId,
        attachment.FileName,
        attachment.ContentType,
        attachment.Size,
        attachment.Sha256,
        attachment.CreatedAt);
}
