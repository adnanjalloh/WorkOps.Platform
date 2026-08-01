using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Common;
using WorkOps.Application.Idempotency;
using WorkOps.Application.Identity;
using WorkOps.Application.Projects;
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
    public const string TenantIdPropertyAnnotation = "WorkOps:TenantIdProperty";

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

        ConfigureTenantBoundary(
            modelBuilder.Entity<Workspace>(),
            workspace => workspaceContext.CurrentWorkspaceId.HasValue &&
                         workspace.Id == workspaceContext.CurrentWorkspaceId.GetValueOrDefault(),
            nameof(Workspace.Id));
        ConfigureTenantBoundary(
            modelBuilder.Entity<WorkspaceMembership>(),
            membership => workspaceContext.CurrentWorkspaceId.HasValue &&
                          membership.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault(),
            nameof(IWorkspaceOwned.WorkspaceId));
        ConfigureTenantBoundary(
            modelBuilder.Entity<Project>(),
            project => workspaceContext.CurrentWorkspaceId.HasValue &&
                       project.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault(),
            nameof(IWorkspaceOwned.WorkspaceId));
        ConfigureTenantBoundary(
            modelBuilder.Entity<WorkItem>(),
            workItem => workspaceContext.CurrentWorkspaceId.HasValue &&
                        workItem.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault(),
            nameof(IWorkspaceOwned.WorkspaceId));
        ConfigureTenantBoundary(
            modelBuilder.Entity<AuditEvent>(),
            auditEvent => workspaceContext.CurrentWorkspaceId.HasValue &&
                          auditEvent.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault(),
            nameof(IWorkspaceOwned.WorkspaceId));
        ConfigureTenantBoundary(
            modelBuilder.Entity<OutboxMessage>(),
            message => workspaceContext.CurrentWorkspaceId.HasValue &&
                       message.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault(),
            nameof(IWorkspaceOwned.WorkspaceId));
        ConfigureTenantBoundary(
            modelBuilder.Entity<InboxMessage>(),
            message => workspaceContext.CurrentWorkspaceId.HasValue &&
                       message.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault(),
            nameof(IWorkspaceOwned.WorkspaceId));
        ConfigureTenantBoundary(
            modelBuilder.Entity<NotificationDelivery>(),
            delivery => workspaceContext.CurrentWorkspaceId.HasValue &&
                        delivery.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault(),
            nameof(IWorkspaceOwned.WorkspaceId));
        ConfigureTenantBoundary(
            modelBuilder.Entity<WorkspaceSubscription>(),
            subscription => workspaceContext.CurrentWorkspaceId.HasValue &&
                            subscription.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault(),
            nameof(IWorkspaceOwned.WorkspaceId));
        ConfigureTenantBoundary(
            modelBuilder.Entity<Attachment>(),
            attachment => workspaceContext.CurrentWorkspaceId.HasValue &&
                          attachment.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault(),
            nameof(IWorkspaceOwned.WorkspaceId));
        ConfigureTenantBoundary(
            modelBuilder.Entity<IdempotencyRecord>(),
            record => workspaceContext.CurrentWorkspaceId.HasValue &&
                      record.WorkspaceId == workspaceContext.CurrentWorkspaceId.GetValueOrDefault(),
            nameof(IWorkspaceOwned.WorkspaceId));
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
        catch (DbUpdateException exception)
        {
            var mappedException = MapKnownConstraint(exception);
            if (mappedException is not null)
            {
                throw mappedException;
            }

            throw;
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
        catch (DbUpdateException exception)
        {
            var mappedException = MapKnownConstraint(exception);
            if (mappedException is not null)
            {
                throw mappedException;
            }

            throw;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var strategy = Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private void EnforceTenantWriteBoundary()
    {
        var pendingEntries = ChangeTracker.Entries()
            .Where(static entry =>
                (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) &&
                entry.Metadata.FindAnnotation(TenantIdPropertyAnnotation)?.Value is string)
            .ToArray();

        if (pendingEntries.Length == 0)
        {
            return;
        }

        foreach (var entry in pendingEntries)
        {
            var tenantIdPropertyName = (string)entry.Metadata
                .FindAnnotation(TenantIdPropertyAnnotation)!
                .Value!;
            var tenantIdProperty = entry.Property(tenantIdPropertyName);
            if (tenantIdProperty.CurrentValue is not WorkspaceId currentTenantId)
            {
                throw new TenantWriteBoundaryException();
            }

            var isWorkspaceRoot = entry.Metadata.ClrType == typeof(Workspace);
            if (isWorkspaceRoot && entry.State == EntityState.Added)
            {
                if (workspaceContext.CurrentWorkspaceId.HasValue ||
                    workspaceContext.ProvisioningWorkspaceId != currentTenantId)
                {
                    throw new TenantWriteBoundaryException();
                }

                continue;
            }

            var allowedWorkspaceId = isWorkspaceRoot
                ? workspaceContext.CurrentWorkspaceId
                : workspaceContext.CurrentWorkspaceId ?? workspaceContext.ProvisioningWorkspaceId;
            if (allowedWorkspaceId != currentTenantId ||
                entry.State is not EntityState.Added &&
                (tenantIdProperty.IsModified ||
                 tenantIdProperty.OriginalValue is not WorkspaceId originalTenantId ||
                 originalTenantId != currentTenantId))
            {
                throw new TenantWriteBoundaryException();
            }
        }
    }

    private static void ConfigureTenantBoundary<TEntity>(
        EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, bool>> queryFilter,
        string tenantIdPropertyName)
        where TEntity : class
    {
        builder.HasQueryFilter(queryFilter);
        builder.Metadata.SetAnnotation(TenantIdPropertyAnnotation, tenantIdPropertyName);
    }

    private static Exception? MapKnownConstraint(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            } postgresException)
        {
            return null;
        }

        return postgresException.ConstraintName switch
        {
            "UX_identity_users_subject" => new DuplicateIdentitySubjectException(),
            "UX_workspaces_slug" => new DuplicateWorkspaceSlugException(),
            "PK_workspace_memberships" => new DuplicateWorkspaceMembershipException(),
            "UX_projects_workspace_key" => new DuplicateProjectKeyException(),
            "PK_idempotency_records" => new IdempotencyRaceException(),
            _ => null,
        };
    }
}
