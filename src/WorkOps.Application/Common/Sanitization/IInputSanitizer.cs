namespace WorkOps.Application.Common.Sanitization;

public interface IInputSanitizer
{
    string Apply(string? value, InputProfile profile, string path);
}
