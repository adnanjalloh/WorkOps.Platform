using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Domain.Identity;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Identity;

internal sealed class UserStore(WorkOpsDbContext dbContext) : IUserStore
{
    public Task<ApplicationUser?> FindBySubjectAsync(
        string subject,
        CancellationToken cancellationToken) => dbContext.Users.SingleOrDefaultAsync(
            user => user.Subject == subject,
            cancellationToken);

    public void Add(ApplicationUser user) => dbContext.Users.Add(user);
}
