using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Identity;
using WorkOps.Application.Tenancy;
using WorkOps.Domain;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Tenancy;

internal sealed class WorkspaceAccessReader(WorkOpsDbContext dbContext) : IWorkspaceAccessReader
{
    public Task<WorkspaceAccess?> FindAsync(
        string subject,
        WorkspaceId workspaceId,
        CancellationToken cancellationToken) => (
            from membership in dbContext.WorkspaceMemberships.IgnoreQueryFilters().AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            join workspace in dbContext.Workspaces.IgnoreQueryFilters().AsNoTracking()
                on membership.WorkspaceId equals workspace.Id
            where user.Subject == subject &&
                  membership.WorkspaceId == workspaceId &&
                  membership.IsActive
            select new WorkspaceAccess(
                user.Id,
                workspace.Id,
                membership.Role,
                workspace.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MembershipView>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken) => await (
            from membership in dbContext.WorkspaceMemberships.IgnoreQueryFilters().AsNoTracking()
            join workspace in dbContext.Workspaces.IgnoreQueryFilters().AsNoTracking()
                on membership.WorkspaceId equals workspace.Id
            where membership.UserId == userId && membership.IsActive
            orderby workspace.Name, workspace.Id
            select new MembershipView(
                workspace.Id,
                workspace.Name,
                workspace.Slug,
                workspace.Status,
                membership.Role))
            .ToListAsync(cancellationToken);
}
