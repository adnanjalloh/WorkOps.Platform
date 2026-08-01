namespace WorkOps.Contracts.Identity;

public sealed record MeResponse(
    Guid UserId,
    string DisplayName,
    IReadOnlyList<MembershipResponse> Memberships);

public sealed record MembershipResponse(
    Guid WorkspaceId,
    string WorkspaceName,
    string WorkspaceSlug,
    string WorkspaceStatus,
    string Role);
