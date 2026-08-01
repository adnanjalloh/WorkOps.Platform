namespace WorkOps.Application.Common.Sanitization;

public enum InputProfile
{
    PlainText,
    Identifier,
    KeyPath,
    HeaderValue,
    SensitiveNoMutation,
    NoneTrusted,
}
