using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Tenancy;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Tenancy;

internal sealed class WorkspaceStore(WorkOpsDbContext dbContext) : IWorkspaceStore
{
    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        dbContext.Workspaces
            .IgnoreQueryFilters()
            .AnyAsync(workspace => workspace.Slug == slug, cancellationToken);

    public void Add(Workspace workspace) => dbContext.Workspaces.Add(workspace);

    public void Add(WorkspaceMembership membership) => dbContext.WorkspaceMemberships.Add(membership);

    public Task<WorkspaceMembership?> FindCurrentMembershipAsync(
        Guid userId,
        CancellationToken cancellationToken) => dbContext.WorkspaceMemberships.SingleOrDefaultAsync(
            membership => membership.UserId == userId,
            cancellationToken);

    public Task<bool> IsCurrentMemberActiveAsync(
        Guid userId,
        CancellationToken cancellationToken) => dbContext.WorkspaceMemberships.AnyAsync(
            membership => membership.UserId == userId && membership.IsActive,
            cancellationToken);

    public Task<Workspace?> GetCurrentAsync(CancellationToken cancellationToken) =>
        dbContext.Workspaces
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkspaceMemberView>> ListCurrentMembersAsync(
        CancellationToken cancellationToken) => await (
            from membership in dbContext.WorkspaceMemberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            orderby user.DisplayName, user.Id
            select new WorkspaceMemberView(
                user.Id,
                user.DisplayName,
                membership.Role,
                membership.IsActive))
            .ToListAsync(cancellationToken);
}
