namespace WorkOps.Application.Files;

public sealed class AttachmentContentUnavailableException : Exception
{
    public AttachmentContentUnavailableException()
        : base("Attachment content is unavailable.")
    {
    }
}
