using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
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
    TimeProvider timeProvider,
    ILogger<AttachmentService> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> LogCleanupFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2101, "AttachmentCleanupFailed"),
            "Attachment storage cleanup failed for attachment {AttachmentId}");
    private static readonly Action<ILogger, Guid, Exception?> LogContentUnavailable =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2102, "AttachmentContentUnavailable"),
            "Attachment storage content is unavailable for attachment {AttachmentId}");
    private static readonly Action<ILogger, Guid, Exception?> LogContentCorrupt =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2103, "AttachmentContentCorrupt"),
            "Attachment storage content failed integrity validation for attachment {AttachmentId}");

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
            try
            {
                await storage.DeleteAsync(
                    attachment.WorkspaceId,
                    attachment.StorageName,
                    CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                LogCleanupFailed(logger, attachment.Id, cleanupException);
            }

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

        try
        {
            await using var storedContent = await storage.OpenReadAsync(
                attachment.WorkspaceId,
                attachment.StorageName,
                cancellationToken);
            var bytes = await ReadStoredContentAsync(storedContent, attachment.Size, cancellationToken);
            var sha256 = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(sha256, attachment.Sha256, StringComparison.Ordinal))
            {
                LogContentCorrupt(logger, attachment.Id, null);
                throw new AttachmentContentUnavailableException();
            }

            return new AttachmentDownload(
                new MemoryStream(bytes, writable: false),
                attachment.FileName,
                attachment.ContentType);
        }
        catch (AttachmentContentUnavailableException)
        {
            throw;
        }
        catch (InvalidDataException integrityException)
        {
            LogContentCorrupt(logger, attachment.Id, integrityException);
            throw new AttachmentContentUnavailableException();
        }
        catch (Exception storageException)
            when (storageException is IOException or UnauthorizedAccessException)
        {
            LogContentUnavailable(logger, attachment.Id, storageException);
            throw new AttachmentContentUnavailableException();
        }
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

    private static async Task<byte[]> ReadStoredContentAsync(
        Stream content,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        if (expectedLength is < 1 or > AttachmentPolicy.MaximumBytes)
        {
            throw new InvalidDataException("Attachment metadata length is invalid.");
        }

        await using var buffer = new MemoryStream((int)expectedLength);
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
                    throw new InvalidDataException("Attachment storage content exceeds its bound.");
                }

                await buffer.WriteAsync(rented.AsMemory(0, read), cancellationToken);
            }

            if (buffer.Length != expectedLength)
            {
                throw new InvalidDataException("Attachment storage length does not match metadata.");
            }

            return buffer.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }
}
