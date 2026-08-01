using WorkOps.Contracts.Common;

namespace WorkOps.Contracts.Tenancy;

public sealed record CreateWorkspaceRequest(
    [property: SanitizeAs(SanitizationProfile.PlainText)] string Name,
    [property: SanitizeAs(SanitizationProfile.KeyPath)] string Slug);
