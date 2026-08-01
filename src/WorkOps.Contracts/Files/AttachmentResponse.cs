namespace WorkOps.Contracts.Files;

public sealed record AttachmentResponse(
    Guid Id,
    Guid WorkItemId,
    string FileName,
    string ContentType,
    long Size,
    string Sha256,
    DateTimeOffset CreatedAt);
