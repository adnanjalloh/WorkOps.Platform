using WorkOps.Contracts.Common;

namespace WorkOps.Contracts.WorkItems;

public sealed record CreateWorkItemRequest(
    [property: SanitizeAs(SanitizationProfile.PlainText)] string Title,
    [property: SanitizeAs(SanitizationProfile.Identifier)] string Priority,
    [property: SkipSanitization(Reason = "A nullable Guid is parsed by the JSON binder and verified as an active workspace member before use.")]
    Guid? AssigneeUserId,
    [property: SanitizeAs(SanitizationProfile.KeyPath)] IReadOnlyList<string> Labels);
