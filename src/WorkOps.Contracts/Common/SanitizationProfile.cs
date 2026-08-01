namespace WorkOps.Contracts.Common;

public enum SanitizationProfile
{
    PlainText,
    SearchText,
    Identifier,
    KeyPath,
    HeaderValue,
    FileName,
    MimeType,
    SensitiveNoMutation,
    NoneTrusted,
}
