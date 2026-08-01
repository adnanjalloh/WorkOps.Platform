using WorkOps.Application.Abstractions;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Domain.Identity;

namespace WorkOps.Application.Identity;

public sealed class IdentityService(
    IUserStore users,
    IWorkspaceAccessReader accessReader,
    IUnitOfWork unitOfWork,
    IInputSanitizer sanitizer,
    TimeProvider timeProvider)
{
    public async Task<(ApplicationUser User, IReadOnlyList<MembershipView> Memberships)> GetMeAsync(
        CurrentIdentity identity,
        CancellationToken cancellationToken)
    {
        var user = await GetOrCreateAsync(identity, cancellationToken);
        var memberships = await accessReader.ListForUserAsync(user.Id, cancellationToken);
        return (user, memberships);
    }

    public async Task<ApplicationUser> GetOrCreateAsync(
        CurrentIdentity identity,
        CancellationToken cancellationToken)
    {
        var subject = sanitizer.Apply(identity.Subject, InputProfile.Identifier, "token.sub");
        var displayName = sanitizer.Apply(identity.DisplayName, InputProfile.PlainText, "token.name");
        var now = timeProvider.GetUtcNow();
        var user = await users.FindBySubjectAsync(subject, cancellationToken);

        if (user is null)
        {
            user = ApplicationUser.Create(subject, displayName, now);
            users.Add(user);
        }
        else
        {
            user.UpdateDisplayName(displayName, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user;
    }
}
