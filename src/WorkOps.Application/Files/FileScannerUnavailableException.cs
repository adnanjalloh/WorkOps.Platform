namespace WorkOps.Application.Files;

public sealed class FileScannerUnavailableException : Exception
{
    public FileScannerUnavailableException()
        : base("The file scanner is unavailable.")
    {
    }
}
