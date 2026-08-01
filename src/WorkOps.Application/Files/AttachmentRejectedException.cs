namespace WorkOps.Application.Files;

public sealed class AttachmentRejectedException(string code) : Exception("Attachment was rejected.")
{
    public string Code { get; } = code;
}
