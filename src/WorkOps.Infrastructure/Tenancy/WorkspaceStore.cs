using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Tenancy;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Tenancy;

internal sealed class WorkspaceStore(
    WorkOpsDbContext dbContext,
    IWorkspaceContextAccessor workspaceContext) : IWorkspaceStore
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

    public Task<Workspace?> LockCurrentForMembershipChangeAsync(CancellationToken cancellationToken)
    {
        var workspaceId = workspaceContext.CurrentWorkspaceId
            ?? throw new InvalidOperationException("Workspace context is required.");
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Membership changes require a transaction.");
        }

        // Serialize lifecycle writes, without blocking unrelated foreign-key checks on the workspace.
        // Keep the explicit key predicate AND the global tenant filter; never lock every workspace.
        return dbContext.Workspaces
            .FromSqlInterpolated($"SELECT * FROM workspaces WHERE \"Id\" = {workspaceId.Value} FOR NO KEY UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<WorkspaceMemberView?> GetCurrentMemberAsync(
        Guid userId,
        CancellationToken cancellationToken) => (
            from membership in dbContext.WorkspaceMemberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.UserId == userId
            select new WorkspaceMemberView(
                user.Id, user.DisplayName, membership.Role, membership.IsActive, membership.Version))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasOtherActiveOwnerAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.WorkspaceMemberships.AnyAsync(
            membership => membership.UserId != userId && membership.IsActive && membership.Role == WorkspaceRole.Owner,
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
                membership.IsActive,
                membership.Version))
            .ToListAsync(cancellationToken);
}
