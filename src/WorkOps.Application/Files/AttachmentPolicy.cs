using System.Text;

namespace WorkOps.Application.Files;

public static class AttachmentPolicy
{
    public const int MaximumBytes = 524_288;

    public static void Validate(
        string fileName,
        string contentType,
        ReadOnlySpan<byte> content)
    {
        if (content.Length is < 1 or > MaximumBytes)
        {
            throw new AttachmentRejectedException("invalid_attachment_size");
        }

        var extension = Path.GetExtension(fileName);
        var valid = extension.ToLowerInvariant() switch
        {
            ".pdf" => contentType == "application/pdf" &&
                      content.StartsWith("%PDF-"u8),
            ".png" => contentType == "image/png" &&
                      content.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".txt" => contentType == "text/plain" && IsSafeText(content),
            _ => false,
        };

        if (!valid)
        {
            throw new AttachmentRejectedException("invalid_attachment_type");
        }
    }

    private static bool IsSafeText(ReadOnlySpan<byte> content)
    {
        try
        {
            var text = new UTF8Encoding(false, true).GetString(content);
            return text.All(static character =>
                !char.IsControl(character) || character is '\r' or '\n' or '\t');
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
