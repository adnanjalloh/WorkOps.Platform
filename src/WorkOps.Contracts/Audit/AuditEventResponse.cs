namespace WorkOps.Contracts.Audit;

public sealed record AuditEventResponse(
    Guid Id,
    Guid ActorUserId,
    string Action,
    string EntityType,
    Guid EntityId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    IReadOnlyDictionary<string, string> Metadata);
