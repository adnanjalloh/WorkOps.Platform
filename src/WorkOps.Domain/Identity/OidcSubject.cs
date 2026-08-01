namespace WorkOps.Domain.Identity;

public static class OidcSubject
{
    public const int MaximumLength = 255;
    public const char MinimumCharacter = '\u0020';
    public const char MaximumCharacter = '\u007e';

    public static bool IsValid(string? value) =>
        value is { Length: > 0 and <= MaximumLength } &&
        value.All(static character => character is >= MinimumCharacter and <= MaximumCharacter);
}
