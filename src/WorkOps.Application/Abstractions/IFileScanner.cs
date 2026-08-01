namespace WorkOps.Application.Abstractions;

public interface IFileScanner
{
    Task<FileScanResult> ScanAsync(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);
}

public enum FileScanResult
{
    Clean,
    Rejected,
    Unavailable,
}
