namespace WorkOps.Contracts.Identity;

public sealed record CapabilitiesResponse(
    Guid WorkspaceId,
    string Role,
    IReadOnlyList<string> Permissions);
