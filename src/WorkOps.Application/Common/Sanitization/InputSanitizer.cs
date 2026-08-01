using System.Globalization;
using System.Text;

namespace WorkOps.Application.Common.Sanitization;

public sealed class InputSanitizer : IInputSanitizer
{
    public string Apply(string? value, InputProfile profile, string path)
    {
        var submittedLength = value?.Length ?? 0;
        var normalized = value?.Normalize(NormalizationForm.FormKC).Trim() ?? string.Empty;

        var accepted = profile switch
        {
            InputProfile.PlainText => IsPlainText(normalized),
            InputProfile.SearchText => IsSearchText(normalized),
            InputProfile.Identifier => IsIdentifier(normalized),
            InputProfile.KeyPath => IsKeyPath(normalized),
            InputProfile.HeaderValue => IsHeaderValue(normalized),
            InputProfile.FileName => IsFileName(normalized),
            InputProfile.MimeType => IsMimeType(normalized),
            InputProfile.SensitiveNoMutation => normalized.Length is > 0 and <= 4096,
            InputProfile.NoneTrusted => true,
            _ => false,
        };

        if (!accepted)
        {
            throw new InputRejectedException(path, profile, submittedLength);
        }

        return profile == InputProfile.KeyPath
            ? normalized.ToLower(CultureInfo.InvariantCulture)
            : normalized;
    }

    private static bool IsPlainText(string value) =>
        value.Length is > 0 and <= 120 &&
        value.All(static character =>
            !char.IsControl(character) && character is not '<' and not '>');

    private static bool IsSearchText(string value) =>
        value.Length is > 0 and <= 120 &&
        value.All(static character =>
            !char.IsControl(character) && character is not '<' and not '>' and not '%' and not '_');

    private static bool IsIdentifier(string value) =>
        value.Length is > 0 and <= 200 &&
        value.All(static character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':' or '@' or '|' or '/');

    private static bool IsKeyPath(string value)
    {
        if (value.Length is < 3 or > 64 || value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        var previousWasSeparator = false;
        foreach (var character in value)
        {
            if (character == '-')
            {
                if (previousWasSeparator)
                {
                    return false;
                }

                previousWasSeparator = true;
                continue;
            }

            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }

            previousWasSeparator = false;
        }

        return true;
    }

    private static bool IsHeaderValue(string value) =>
        value.Length is > 0 and <= 128 &&
        value.All(static character => !char.IsControl(character));

    private static bool IsFileName(string value) =>
        value.Length is > 0 and <= 120 &&
        value[0] != '.' &&
        Path.GetFileName(value) == value &&
        value.All(static character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is ' ' or '-' or '_' or '.');

    private static bool IsMimeType(string value)
    {
        if (value.Length is < 3 or > 100 || value.Count(static character => character == '/') != 1)
        {
            return false;
        }

        return value.All(static character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '+' or '.' or '/');
    }
}
