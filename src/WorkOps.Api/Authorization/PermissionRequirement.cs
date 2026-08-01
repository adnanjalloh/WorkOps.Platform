using Microsoft.AspNetCore.Authorization;

namespace WorkOps.Api.Authorization;

internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
