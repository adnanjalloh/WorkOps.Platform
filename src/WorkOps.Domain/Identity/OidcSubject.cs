namespace WorkOps.Domain.Identity;

public static class OidcSubject
{
    public const int MaximumLength = 255;

    public static bool IsValid(string? value) =>
        value is { Length: > 0 and <= MaximumLength } &&
        value.All(static character => character <= 0x7f);
}
