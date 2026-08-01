namespace WorkOps.Application.Files;

public sealed record AttachmentView(
    Guid Id,
    Guid WorkItemId,
    string FileName,
    string ContentType,
    long Size,
    string Sha256,
    DateTimeOffset CreatedAt);
