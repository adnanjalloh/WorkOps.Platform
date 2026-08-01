using WorkOps.Domain.Common;

namespace WorkOps.Domain.Idempotency;

public sealed class IdempotencyRecord : IWorkspaceOwned
{
    private IdempotencyRecord()
    {
    }

    private IdempotencyRecord(
        WorkspaceId workspaceId,
        Guid userId,
        string method,
        string route,
        string key,
        string requestHash,
        int statusCode,
        string responseBodyJson,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Method = method;
        Route = route;
        Key = key;
        RequestHash = requestHash;
        StatusCode = statusCode;
        ResponseBodyJson = responseBodyJson;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public WorkspaceId WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public string Method { get; private set; } = string.Empty;

    public string Route { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public int StatusCode { get; private set; }

    public string ResponseBodyJson { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public static IdempotencyRecord Create(
        WorkspaceId workspaceId,
        Guid userId,
        string method,
        string route,
        string key,
        string requestHash,
        int statusCode,
        string responseBodyJson,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) => new(
            workspaceId,
            userId,
            method,
            route,
            key,
            requestHash,
            statusCode,
            responseBodyJson,
            createdAt,
            expiresAt);

    public void Replace(
        string requestHash,
        int statusCode,
        string responseBodyJson,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        RequestHash = requestHash;
        StatusCode = statusCode;
        ResponseBodyJson = responseBodyJson;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }
}
