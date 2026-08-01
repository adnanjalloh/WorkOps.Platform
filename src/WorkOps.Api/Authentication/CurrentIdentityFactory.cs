using System.Security.Claims;
using WorkOps.Application.Abstractions;
using WorkOps.Domain.Identity;

namespace WorkOps.Api.Authentication;

internal static class CurrentIdentityFactory
{
    public static CurrentIdentity Create(ClaimsPrincipal principal)
    {
        var subjects = principal.FindAll("sub").Select(static claim => claim.Value).ToArray();
        if (subjects.Length != 1 || !OidcSubject.IsValid(subjects[0]))
        {
            throw new InvalidOperationException("The validated token subject invariant was not satisfied.");
        }

        var subject = subjects[0];
        var displayName = principal.FindFirstValue("name")
            ?? principal.FindFirstValue("preferred_username")
            ?? "Authenticated user";

        return new CurrentIdentity(subject, displayName);
    }
}
