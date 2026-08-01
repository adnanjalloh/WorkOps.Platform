using WorkOps.Domain;

namespace WorkOps.Application.Abstractions;

public interface IFileStorage
{
    Task SaveAsync(
        WorkspaceId workspaceId,
        string storageName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        WorkspaceId workspaceId,
        string storageName,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        WorkspaceId workspaceId,
        string storageName,
        CancellationToken cancellationToken);
}
