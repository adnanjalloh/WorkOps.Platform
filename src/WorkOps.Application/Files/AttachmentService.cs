using System.Buffers;
using System.Security.Cryptography;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Files;

namespace WorkOps.Application.Files;

public sealed class AttachmentService(
    IWorkItemStore workItems,
    IAttachmentStore attachments,
    IFileScanner scanner,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    IWorkspaceContextAccessor workspaceContext,
    AuditWriter auditWriter,
    IInputSanitizer sanitizer,
    TimeProvider timeProvider)
{
    public async Task<AttachmentView?> UploadAsync(
        Guid workItemId,
        string fileName,
        string contentType,
        long declaredLength,
        Stream content,
        CancellationToken cancellationToken)
    {
        var workItem = await workItems.FindAsync(workItemId, cancellationToken);
        if (workItem is null)
        {
            return null;
        }

        var current = workspaceContext.Current
            ?? throw new InvalidOperationException("An interactive workspace context is required.");
        var safeFileName = sanitizer.Apply(fileName, InputProfile.FileName, "form.file.fileName");
        var safeContentType = sanitizer.Apply(contentType, InputProfile.MimeType, "form.file.contentType");
        if (declaredLength is < 1 or > AttachmentPolicy.MaximumBytes)
        {
            throw new AttachmentRejectedException("invalid_attachment_size");
        }

        var bytes = await ReadBoundedAsync(content, cancellationToken);
        AttachmentPolicy.Validate(safeFileName, safeContentType, bytes.Span);
        var scanResult = await scanner.ScanAsync(bytes, cancellationToken);
        if (scanResult == FileScanResult.Unavailable)
        {
            throw new FileScannerUnavailableException();
        }

        if (scanResult != FileScanResult.Clean)
        {
            throw new AttachmentRejectedException("attachment_scan_rejected");
        }

        var now = timeProvider.GetUtcNow();
        var attachment = Attachment.Create(
            current.WorkspaceId,
            workItem.Id,
            current.UserId,
            safeFileName,
            safeContentType,
            bytes.Length,
            Convert.ToHexString(SHA256.HashData(bytes.Span)),
            now);
        await storage.SaveAsync(
            attachment.WorkspaceId,
            attachment.StorageName,
            bytes,
            cancellationToken);
        attachments.Add(attachment);
        auditWriter.Record(
            AuditActions.AttachmentUploaded,
            "attachment",
            attachment.Id,
            now,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["contentType"] = attachment.ContentType,
                ["extension"] = Path.GetExtension(attachment.FileName).ToLowerInvariant(),
                ["size"] = attachment.Size.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(
                attachment.WorkspaceId,
                attachment.StorageName,
                CancellationToken.None);
            throw;
        }

        return await attachments.GetAsync(attachment.Id, cancellationToken)
            ?? throw new InvalidOperationException("Created attachment could not be read.");
    }

    public async Task<AttachmentDownload?> DownloadAsync(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await attachments.FindAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return null;
        }

        var stream = await storage.OpenReadAsync(
            attachment.WorkspaceId,
            attachment.StorageName,
            cancellationToken);
        return new AttachmentDownload(stream, attachment.FileName, attachment.ContentType);
    }

    private static async Task<ReadOnlyMemory<byte>> ReadBoundedAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var rented = ArrayPool<byte>.Shared.Rent(81_920);
        try
        {
            while (true)
            {
                var read = await content.ReadAsync(rented, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                if (buffer.Length + read > AttachmentPolicy.MaximumBytes)
                {
                    throw new AttachmentRejectedException("invalid_attachment_size");
                }

                await buffer.WriteAsync(rented.AsMemory(0, read), cancellationToken);
            }

            return buffer.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }
}
