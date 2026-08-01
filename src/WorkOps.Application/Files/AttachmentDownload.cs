namespace WorkOps.Application.Files;

public sealed record AttachmentDownload(
    Stream Content,
    string FileName,
    string ContentType);
