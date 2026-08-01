using WorkOps.Contracts.Common;

namespace WorkOps.Contracts.Projects;

public sealed record CreateProjectRequest(
    [property: SanitizeAs(SanitizationProfile.PlainText)] string Name,
    [property: SanitizeAs(SanitizationProfile.KeyPath)] string Key);
