namespace WorkOps.Application.Common.Sanitization;

public sealed class InputRejectedException(
    string path,
    InputProfile profile,
    int submittedLength) : Exception("Submitted input did not satisfy its sanitization policy.")
{
    public string Path { get; } = path;

    public InputProfile Profile { get; } = profile;

    public int SubmittedLength { get; } = submittedLength;
}
