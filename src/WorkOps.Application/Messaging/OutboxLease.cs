using WorkOps.Domain;

namespace WorkOps.Application.Messaging;

public sealed record OutboxLease(
    Guid Id,
    WorkspaceId WorkspaceId,
    string Type,
    string PayloadJson,
    int AttemptCount,
    DateTimeOffset OccurredAt);
