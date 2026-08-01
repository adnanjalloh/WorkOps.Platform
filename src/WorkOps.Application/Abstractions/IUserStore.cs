using WorkOps.Domain.Identity;

namespace WorkOps.Application.Abstractions;

public interface IUserStore
{
    Task<ApplicationUser?> FindBySubjectAsync(string subject, CancellationToken cancellationToken);

    Task<ApplicationUser> GetOrCreateAsync(
        string subject,
        string displayName,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
