using WorkOps.Domain.Identity;

namespace WorkOps.Application.Abstractions;

public interface IUserStore
{
    Task<ApplicationUser?> FindBySubjectAsync(string subject, CancellationToken cancellationToken);

    void Add(ApplicationUser user);
}
