using WorkOps.Application.Abstractions;

namespace WorkOps.Infrastructure.Files;

internal sealed class RejectingFileScanner : IFileScanner
{
    public Task<FileScanResult> ScanAsync(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        _ = content;
        return Task.FromResult(FileScanResult.Unavailable);
    }
}
