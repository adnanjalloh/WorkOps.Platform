using WorkOps.Application.Abstractions;
using WorkOps.Domain;

namespace WorkOps.Infrastructure.Files;

internal sealed class LocalFileStorage(string rootPath) : IFileStorage
{
    private readonly string _rootPath = Path.IsPathFullyQualified(rootPath)
        ? rootPath
        : throw new InvalidOperationException("Files:RootPath must be absolute.");

    public async Task SaveAsync(
        WorkspaceId workspaceId,
        string storageName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(workspaceId, storageName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81_920,
            useAsync: true);
        await stream.WriteAsync(content, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(
        WorkspaceId workspaceId,
        string storageName,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        Stream stream = new FileStream(
            ResolvePath(workspaceId, storageName),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81_920,
            useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        WorkspaceId workspaceId,
        string storageName,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var path = ResolvePath(workspaceId, storageName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(WorkspaceId workspaceId, string storageName)
    {
        if (!IsStorageName(storageName))
        {
            throw new InvalidOperationException("The storage name is invalid.");
        }

        return Path.Combine(_rootPath, workspaceId.Value.ToString("N"), storageName);
    }

    private static bool IsStorageName(string value)
    {
        if (value.Length != 36 || !value.EndsWith(".bin", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in value.AsSpan(0, 32))
        {
            if (!char.IsAsciiHexDigit(character) || char.IsUpper(character))
            {
                return false;
            }
        }

        return true;
    }
}
