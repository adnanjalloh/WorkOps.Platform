namespace WorkOps.Contracts.Common;

public enum SanitizationProfile
{
    PlainText,
    Identifier,
    KeyPath,
    HeaderValue,
    SensitiveNoMutation,
    NoneTrusted,
}
