namespace WorkOps.Contracts.Tenancy;

public sealed record WorkspaceMemberResponse(
    Guid UserId,
    string DisplayName,
    string Role,
    bool IsActive);
