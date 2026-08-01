using WorkOps.Contracts.Common;

namespace WorkOps.Contracts.Tenancy;

public sealed record InviteWorkspaceMemberRequest(
    [property: SanitizeAs(SanitizationProfile.Identifier)] string Subject,
    [property: SanitizeAs(SanitizationProfile.PlainText)] string DisplayName,
    [property: SanitizeAs(SanitizationProfile.Identifier)] string Role);
