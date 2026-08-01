using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Identity;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Infrastructure.Persistence;

public sealed class WorkOpsDbContext(
    DbContextOptions<WorkOpsDbContext> options,
    IWorkspaceContextAccessor workspaceContext) : DbContext(options), IUnitOfWork
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkOpsDbContext).Assembly);

        modelBuilder.Entity<Workspace>().HasQueryFilter(
            workspace => workspaceContext.Current != null &&
                         workspace.Id == workspaceContext.Current.WorkspaceId);

        modelBuilder.Entity<WorkspaceMembership>().HasQueryFilter(
            membership => workspaceContext.Current != null &&
                          membership.WorkspaceId == workspaceContext.Current.WorkspaceId);
    }
}
