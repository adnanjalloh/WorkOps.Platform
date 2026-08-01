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

    public async Task<ApplicationUser> GetOrCreateAsync(
        string subject,
        string displayName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidate = ApplicationUser.Create(subject, displayName, now);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity_users ("Id", "Subject", "DisplayName", "CreatedAt", "UpdatedAt")
            VALUES ({candidate.Id}, {candidate.Subject}, {candidate.DisplayName}, {candidate.CreatedAt}, NULL)
            ON CONFLICT ("Subject") DO UPDATE
            SET "UpdatedAt" = CASE
                    WHEN identity_users."DisplayName" IS DISTINCT FROM EXCLUDED."DisplayName"
                    THEN EXCLUDED."CreatedAt"
                    ELSE identity_users."UpdatedAt"
                END,
                "DisplayName" = EXCLUDED."DisplayName"
            """, cancellationToken);

        return await dbContext.Users.SingleAsync(
            user => user.Subject == subject,
            cancellationToken);
    }
}
