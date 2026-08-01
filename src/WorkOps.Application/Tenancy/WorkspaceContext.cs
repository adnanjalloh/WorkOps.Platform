using WorkOps.Domain;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Application.Tenancy;

public sealed record WorkspaceContext(
    Guid UserId,
    WorkspaceId WorkspaceId,
    WorkspaceRole Role,
    WorkspaceStatus Status);
