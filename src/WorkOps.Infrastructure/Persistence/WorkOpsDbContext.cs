using Microsoft.EntityFrameworkCore;
using Npgsql;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Common;
using WorkOps.Application.Idempotency;
using WorkOps.Application.Tenancy;
using WorkOps.Domain;
using WorkOps.Domain.Audit;
using WorkOps.Domain.Common;
using WorkOps.Domain.Features;
using WorkOps.Domain.Files;
using WorkOps.Domain.Idempotency;
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

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

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

        modelBuilder.Entity<IdempotencyRecord>().HasQueryFilter(
            record => workspaceContext.CurrentWorkspaceId.HasValue &&
                      record.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault());
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantWriteBoundary();

        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "PK_idempotency_records",
            })
        {
            throw new IdempotencyRaceException();
        }
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceTenantWriteBoundary();

        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "PK_idempotency_records",
            })
        {
            throw new IdempotencyRaceException();
        }
    }

    private void EnforceTenantWriteBoundary()
    {
        var pendingEntries = ChangeTracker.Entries()
            .Where(static entry =>
                entry.Entity is IWorkspaceOwned &&
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();

        if (pendingEntries.Length == 0)
        {
            return;
        }

        var allowedWorkspaceId = workspaceContext.CurrentWorkspaceId ??
                                 workspaceContext.ProvisioningWorkspaceId;
        if (!allowedWorkspaceId.HasValue)
        {
            throw new TenantWriteBoundaryException();
        }

        foreach (var entry in pendingEntries)
        {
            var entity = (IWorkspaceOwned)entry.Entity;
            var workspaceProperty = entry.Property(nameof(IWorkspaceOwned.WorkspaceId));

            if (entity.WorkspaceId != allowedWorkspaceId.Value ||
                entry.State is not EntityState.Added &&
                (workspaceProperty.IsModified ||
                 workspaceProperty.OriginalValue is not WorkspaceId originalWorkspaceId ||
                 originalWorkspaceId != allowedWorkspaceId.Value))
            {
                throw new TenantWriteBoundaryException();
            }
        }
    }
}
