using WorkOps.Domain;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Application.Tenancy;

public sealed record WorkspaceAccess(
    Guid UserId,
    WorkspaceId WorkspaceId,
    WorkspaceRole Role,
    WorkspaceStatus Status);
