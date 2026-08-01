using System.Text.Json;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Tenancy;
using WorkOps.Domain;
using WorkOps.Domain.Audit;

namespace WorkOps.Application.Audit;

public sealed class AuditWriter(
    IAuditStore auditStore,
    IWorkspaceContextAccessor workspaceContext,
    ICorrelationContext correlationContext)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Record(
        string action,
        string entityType,
        Guid entityId,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, string> metadata)
    {
        var current = workspaceContext.Current
            ?? throw new InvalidOperationException("An interactive workspace context is required.");
        RecordFor(
            current.WorkspaceId,
            current.UserId,
            action,
            entityType,
            entityId,
            occurredAt,
            metadata);
    }

    public void RecordFor(
        WorkspaceId workspaceId,
        Guid actorUserId,
        string action,
        string entityType,
        Guid entityId,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, string> metadata)
    {
        var orderedMetadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in metadata)
        {
            orderedMetadata.Add(item.Key, item.Value);
        }
        auditStore.Add(AuditEvent.Record(
            workspaceId,
            actorUserId,
            action,
            entityType,
            entityId,
            occurredAt,
            correlationContext.CorrelationId,
            JsonSerializer.Serialize(orderedMetadata, SerializerOptions)));
    }
}
