namespace WorkOps.Application.Audit;

public sealed record AuditEventView(
    Guid Id,
    Guid ActorUserId,
    string Action,
    string EntityType,
    Guid EntityId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string MetadataJson);
