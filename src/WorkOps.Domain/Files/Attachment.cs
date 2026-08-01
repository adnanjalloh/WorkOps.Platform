using WorkOps.Domain.Common;

namespace WorkOps.Domain.Files;

public sealed class Attachment : IWorkspaceOwned
{
    private Attachment()
    {
    }

    private Attachment(
        Guid id,
        WorkspaceId workspaceId,
        Guid workItemId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        long size,
        string storageName,
        string sha256,
        DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        WorkItemId = workItemId;
        UploadedByUserId = uploadedByUserId;
        FileName = fileName;
        ContentType = contentType;
        Size = size;
        StorageName = storageName;
        Sha256 = sha256;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public WorkspaceId WorkspaceId { get; private set; }

    public Guid WorkItemId { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public string StorageName { get; private set; } = string.Empty;

    public string Sha256 { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Attachment Create(
        WorkspaceId workspaceId,
        Guid workItemId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        long size,
        string sha256,
        DateTimeOffset createdAt)
    {
        var id = Guid.NewGuid();
        return new Attachment(
            id,
            workspaceId,
            workItemId,
            uploadedByUserId,
            fileName,
            contentType,
            size,
            $"{id:N}.bin",
            sha256,
            createdAt);
    }
}
