using System.Security.Claims;
using WorkOps.Application.Abstractions;

namespace WorkOps.Api.Authentication;

internal static class CurrentIdentityFactory
{
    public static CurrentIdentity Create(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("The validated token does not contain a subject.");
        var displayName = principal.FindFirstValue("name")
            ?? principal.FindFirstValue("preferred_username")
            ?? "Authenticated user";

        return new CurrentIdentity(subject, displayName);
    }
}
