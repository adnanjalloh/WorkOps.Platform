using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Common;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Audit;
using WorkOps.Domain.Features;
using WorkOps.Domain.Files;
using WorkOps.Domain.Identity;
using WorkOps.Domain.Messaging;
using WorkOps.Domain.Notifications;
using WorkOps.Domain.Projects;
using WorkOps.Domain.Tenancy;
using WorkOps.Domain.WorkItems;

namespace WorkOps.Infrastructure.Persistence;

public sealed class WorkOpsDbContext(
    DbContextOptions<WorkOpsDbContext> options,
    IWorkspaceContextAccessor workspaceContext) : DbContext(options), IUnitOfWork
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    public DbSet<WorkspaceSubscription> WorkspaceSubscriptions => Set<WorkspaceSubscription>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkOpsDbContext).Assembly);

        modelBuilder.Entity<Workspace>().HasQueryFilter(
            workspace => workspaceContext.CurrentWorkspaceId.HasValue &&
                         workspace.Id == workspaceContext.CurrentWorkspaceId.GetValueOrDefault());

        modelBuilder.Entity<WorkspaceMembership>().HasQueryFilter(
            membership => workspaceContext.CurrentWorkspaceId.HasValue &&
                          membership.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault());

        modelBuilder.Entity<Project>().HasQueryFilter(
            project => workspaceContext.CurrentWorkspaceId.HasValue &&
                       project.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault());

        modelBuilder.Entity<WorkItem>().HasQueryFilter(
            workItem => workspaceContext.CurrentWorkspaceId.HasValue &&
                       workItem.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault());

        modelBuilder.Entity<AuditEvent>().HasQueryFilter(
            auditEvent => workspaceContext.CurrentWorkspaceId.HasValue &&
                          auditEvent.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault());

        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => workspaceContext.CurrentWorkspaceId.HasValue &&
                       message.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault());

        modelBuilder.Entity<InboxMessage>().HasQueryFilter(
            message => workspaceContext.CurrentWorkspaceId.HasValue &&
                       message.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault());

        modelBuilder.Entity<NotificationDelivery>().HasQueryFilter(
            delivery => workspaceContext.CurrentWorkspaceId.HasValue &&
                        delivery.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault());

        modelBuilder.Entity<WorkspaceSubscription>().HasQueryFilter(
            subscription => workspaceContext.CurrentWorkspaceId.HasValue &&
                            subscription.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault());

        modelBuilder.Entity<Attachment>().HasQueryFilter(
            attachment => workspaceContext.CurrentWorkspaceId.HasValue &&
                          attachment.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
    }
}
