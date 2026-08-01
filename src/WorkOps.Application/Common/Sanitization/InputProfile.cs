namespace WorkOps.Application.Common.Sanitization;

public enum InputProfile
{
    PlainText,
    SearchText,
    Identifier,
    KeyPath,
    HeaderValue,
    SensitiveNoMutation,
    NoneTrusted,
}
