using WorkOps.Contracts.Common;

namespace WorkOps.Contracts.Tenancy;

public sealed record DeactivateWorkspaceMemberRequest(
    [property: SkipSanitization(Reason = "An opaque version is validated as exactly eight ASCII hexadecimal characters without mutation.")]
    string ExpectedVersion);
