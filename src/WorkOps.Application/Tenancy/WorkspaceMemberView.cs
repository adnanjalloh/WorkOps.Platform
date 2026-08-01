using WorkOps.Domain.Tenancy;

namespace WorkOps.Application.Tenancy;

public sealed record WorkspaceMemberView(
    Guid UserId,
    string DisplayName,
    WorkspaceRole Role,
    bool IsActive);
