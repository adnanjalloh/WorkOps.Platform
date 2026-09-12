using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common;
using WorkOps.Application.Tenancy;
using WorkOps.Domain;
using WorkOps.Domain.Identity;
using WorkOps.Domain.Tenancy;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.IntegrationTests;

public sealed partial class TenantQueryFilterTests
{
    [TestMethod]
    [DataRow("self-demotion")]
    [DataRow("mutual-demotion")]
    [DataRow("mutual-deactivation")]
    public async Task Membership_concurrent_owner_changes_always_retain_an_active_owner(string scenario)
    {
        var (first, workspace, _) = await SeedProjectAsync("owner-race");
        var second = await SeedMemberAsync(first.Id, workspace.Id, WorkspaceRole.Owner);
        await using var provider = CreateServices(false, interceptor: new MembershipRaceGate());
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstService = MembershipService(firstScope.ServiceProvider, first.Id, workspace.Id, WorkspaceRole.Owner);
        var secondService = MembershipService(secondScope.ServiceProvider, second.UserId, workspace.Id, WorkspaceRole.Owner);
        var firstMember = await ReadMembershipAsync(first.Id, workspace.Id);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var results = await Task.WhenAll(
            CaptureAsync(() => scenario switch
            {
                "self-demotion" => firstService.ChangeRoleAsync(first.Id, "Administrator", firstMember.Version, timeout.Token),
                "mutual-demotion" => firstService.ChangeRoleAsync(second.UserId, "Administrator", second.Version, timeout.Token),
                _ => firstService.DeactivateAsync(second.UserId, second.Version, timeout.Token),
            }),
            CaptureAsync(() => scenario switch
            {
                "self-demotion" => secondService.ChangeRoleAsync(second.UserId, "Administrator", second.Version, timeout.Token),
                "mutual-demotion" => secondService.ChangeRoleAsync(first.Id, "Administrator", firstMember.Version, timeout.Token),
                _ => secondService.DeactivateAsync(first.Id, firstMember.Version, timeout.Token),
            }));

        Assert.AreEqual(1, results.Count(result => result is null));
        var failure = results.Single(result => result is not null);
        Assert.IsTrue(scenario switch
        {
            "self-demotion" => failure is LastWorkspaceOwnerException,
            "mutual-demotion" => failure is MembershipManagementForbiddenException,
            _ => failure is WorkspaceAccessRevokedException,
        }, failure?.ToString());
        await using var verified = CreateDbContext(CreateAccessor(first.Id, workspace.Id));
        Assert.AreEqual(1, await verified.WorkspaceMemberships.CountAsync(row => row.IsActive && row.Role == WorkspaceRole.Owner));
        Assert.AreEqual(1, await verified.AuditEvents.CountAsync(row =>
            row.Action == AuditActions.MemberRoleChanged || row.Action == AuditActions.MemberDeactivated));
    }

    [TestMethod]
    public async Task Membership_concurrent_edits_accept_only_one_expected_version()
    {
        var (owner, workspace, _) = await SeedProjectAsync("member-version");
        var target = await SeedMemberAsync(owner.Id, workspace.Id, WorkspaceRole.Viewer);
        await using var provider = CreateServices(false, interceptor: new MembershipRaceGate());
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var first = MembershipService(firstScope.ServiceProvider, owner.Id, workspace.Id, WorkspaceRole.Owner);
        var second = MembershipService(secondScope.ServiceProvider, owner.Id, workspace.Id, WorkspaceRole.Owner);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var results = await Task.WhenAll(
            CaptureAsync(() => first.ChangeRoleAsync(target.UserId, "Administrator", target.Version, timeout.Token)),
            CaptureAsync(() => second.DeactivateAsync(target.UserId, target.Version, timeout.Token)));
        Assert.AreEqual(1, results.Count(result => result is null));
        Assert.IsInstanceOfType<ConcurrencyConflictException>(results.Single(result => result is not null));
        await using var verified = CreateDbContext(CreateAccessor(owner.Id, workspace.Id));
        Assert.AreEqual(1, await verified.AuditEvents.CountAsync(row => row.EntityId == target.UserId));
    }

    [TestMethod]
    [DataRow("invite", false)]
    [DataRow("change-role", false)]
    [DataRow("deactivate", false)]
    [DataRow("invite", true)]
    [DataRow("change-role", true)]
    [DataRow("deactivate", true)]
    public async Task Membership_waiting_requests_recheck_revoked_authority(string operation, bool deactivateActor)
    {
        var (owner, workspace, _) = await SeedProjectAsync("stale-authority");
        var administrator = await SeedMemberAsync(owner.Id, workspace.Id, WorkspaceRole.Administrator);
        var target = await SeedMemberAsync(owner.Id, workspace.Id, WorkspaceRole.Viewer);
        var pause = new PauseMembershipLock();
        await using var waitingProvider = CreateServices(false, interceptor: pause);
        await using var ownerProvider = CreateServices(false);
        await using var waitingScope = waitingProvider.CreateAsyncScope();
        await using var ownerScope = ownerProvider.CreateAsyncScope();
        var waiting = MembershipService(waitingScope.ServiceProvider, administrator.UserId, workspace.Id, WorkspaceRole.Administrator);
        var ownerService = MembershipService(ownerScope.ServiceProvider, owner.Id, workspace.Id, WorkspaceRole.Owner);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var invitedSubject = $"integration|not-invited-{Guid.NewGuid():N}";
        var pending = CaptureAsync(() => operation switch
        {
            "invite" => (Task)waiting.InviteAsync(invitedSubject, "Not Invited", "Viewer", timeout.Token),
            "change-role" => waiting.ChangeRoleAsync(target.UserId, "ProjectContributor", target.Version, timeout.Token),
            _ => waiting.DeactivateAsync(target.UserId, target.Version, timeout.Token),
        });
        try
        {
            await pause.Reached.Task.WaitAsync(timeout.Token);
            if (deactivateActor)
            {
                await ownerService.DeactivateAsync(administrator.UserId, administrator.Version, timeout.Token);
            }
            else
            {
                await ownerService.ChangeRoleAsync(administrator.UserId, "Viewer", administrator.Version, timeout.Token);
            }
        }
        finally
        {
            pause.Release.TrySetResult();
        }

        var failure = await pending;
        Assert.IsTrue(deactivateActor
            ? failure is WorkspaceAccessRevokedException
            : failure is MembershipManagementForbiddenException, failure?.ToString());
        var unchanged = await ReadMembershipAsync(target.UserId, workspace.Id);
        Assert.AreEqual(target, unchanged);
        await using var verified = CreateDbContext(CreateAccessor(owner.Id, workspace.Id));
        Assert.IsFalse(await verified.Users.AnyAsync(row => row.Subject == invitedSubject));
        Assert.AreEqual(1, await verified.AuditEvents.CountAsync());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Membership_audit_and_mutation_roll_back_after_a_completed_save_fails(bool deactivate)
    {
        var (owner, workspace, _) = await SeedProjectAsync("member-rollback");
        var target = await SeedMemberAsync(owner.Id, workspace.Id, WorkspaceRole.Viewer);
        var failure = new FailAfterMembershipSave();
        await using var provider = CreateServices(false, interceptor: failure);
        await using (var scope = provider.CreateAsyncScope())
        {
            var service = MembershipService(scope.ServiceProvider, owner.Id, workspace.Id, WorkspaceRole.Owner);
            var exception = await CaptureAsync(() => deactivate
                ? service.DeactivateAsync(target.UserId, target.Version, CancellationToken.None)
                : service.ChangeRoleAsync(target.UserId, "Owner", target.Version, CancellationToken.None));
            Assert.IsInstanceOfType<IOException>(exception);
            Assert.IsTrue(failure.Injected);
        }

        Assert.AreEqual(target, await ReadMembershipAsync(target.UserId, workspace.Id));
        await using var verified = CreateDbContext(CreateAccessor(owner.Id, workspace.Id));
        Assert.AreEqual(0, await verified.AuditEvents.CountAsync());
        Assert.AreEqual(1, await verified.WorkspaceMemberships.CountAsync(row => row.IsActive && row.Role == WorkspaceRole.Owner));
    }

    [TestMethod]
    public async Task Membership_lock_is_tenant_scoped_requires_a_transaction_and_releases_on_cancellation()
    {
        var (firstOwner, firstWorkspace, _) = await SeedProjectAsync("lock-first");
        var (secondOwner, secondWorkspace, _) = await SeedProjectAsync("lock-second");
        var firstTarget = await SeedMemberAsync(firstOwner.Id, firstWorkspace.Id, WorkspaceRole.Viewer);
        var secondTarget = await SeedMemberAsync(secondOwner.Id, secondWorkspace.Id, WorkspaceRole.Viewer);
        await using var provider = CreateServices(false);
        await using var holdingScope = provider.CreateAsyncScope();
        _ = MembershipService(holdingScope.ServiceProvider, firstOwner.Id, firstWorkspace.Id, WorkspaceRole.Owner);
        var store = holdingScope.ServiceProvider.GetRequiredService<IWorkspaceStore>();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.LockCurrentForMembershipChangeAsync(CancellationToken.None));
        var database = holdingScope.ServiceProvider.GetRequiredService<WorkOpsDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync();
        Assert.IsNotNull(await store.LockCurrentForMembershipChangeAsync(CancellationToken.None));

        await using (var otherScope = provider.CreateAsyncScope())
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var other = MembershipService(otherScope.ServiceProvider, secondOwner.Id, secondWorkspace.Id, WorkspaceRole.Owner);
            Assert.IsNotNull(await other.ChangeRoleAsync(secondTarget.UserId, "ProjectContributor", secondTarget.Version, timeout.Token));
        }

        var entered = new ObserveMembershipLock();
        await using var waitingProvider = CreateServices(false, interceptor: entered);
        await using (var waitingScope = waitingProvider.CreateAsyncScope())
        {
            using var cancelled = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var waiting = MembershipService(waitingScope.ServiceProvider, firstOwner.Id, firstWorkspace.Id, WorkspaceRole.Owner);
            var pending = CaptureAsync(() => waiting.DeactivateAsync(firstTarget.UserId, firstTarget.Version, cancelled.Token));
            await entered.Reached.Task.WaitAsync(cancelled.Token);
            await cancelled.CancelAsync();
            Assert.IsInstanceOfType<OperationCanceledException>(await pending);
        }

        await transaction.RollbackAsync();
        Assert.AreEqual(firstTarget, await ReadMembershipAsync(firstTarget.UserId, firstWorkspace.Id));
        await using var afterScope = provider.CreateAsyncScope();
        using var afterTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var after = MembershipService(afterScope.ServiceProvider, firstOwner.Id, firstWorkspace.Id, WorkspaceRole.Owner);
        Assert.IsNotNull(await after.DeactivateAsync(firstTarget.UserId, firstTarget.Version, afterTimeout.Token));
    }

    [TestMethod]
    public async Task Membership_migration_preserves_existing_data_and_system_version_across_upgrade_and_rollback()
    {
        await using var upgradeDatabase = new PostgreSqlBuilder("postgres:18.4-alpine").Build();
        await upgradeDatabase.StartAsync();
        var accessor = new WorkspaceContextAccessor();
        var options = new DbContextOptionsBuilder<WorkOpsDbContext>().UseNpgsql(upgradeDatabase.GetConnectionString()).Options;
        await using var context = new WorkOpsDbContext(options, accessor);
        var migrator = context.GetService<IMigrator>();
        const string previousMigration = "20260911162012_WorkItemListingIndexes";
        await migrator.MigrateAsync(previousMigration);
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create($"integration|upgrade-{Guid.NewGuid():N}", "Upgrade Member", now);
        var workspace = Workspace.Create("Upgrade Workspace", $"upgrade-{Guid.NewGuid():N}", now);
        var member = WorkspaceMembership.Create(workspace.Id, user.Id, WorkspaceRole.Owner, now);
        using (accessor.BeginProvisioning(workspace.Id))
        {
            context.Users.Add(user);
            context.Workspaces.Add(workspace);
            context.WorkspaceMemberships.Add(member);
            await context.SaveChangesAsync();
        }

        var version = member.Version;
        Assert.AreNotEqual(0u, version);
        accessor.EstablishBackground(workspace.Id);
        foreach (var migration in new[] { "20260912132453_MembershipConcurrency", previousMigration, "20260912132453_MembershipConcurrency" })
        {
            await migrator.MigrateAsync(migration);
            context.ChangeTracker.Clear();
            var persisted = await context.WorkspaceMemberships.SingleAsync();
            Assert.AreEqual(user.Id, persisted.UserId);
            Assert.AreEqual(WorkspaceRole.Owner, persisted.Role);
            Assert.IsTrue(persisted.IsActive);
            Assert.AreEqual(version, persisted.Version);
        }

        var updated = await context.WorkspaceMemberships.SingleAsync();
        updated.ChangeRole(WorkspaceRole.Administrator, now.AddMinutes(1));
        await context.SaveChangesAsync();
        Assert.AreNotEqual(version, updated.Version);
        Assert.IsFalse(context.Database.HasPendingModelChanges());
    }

    [TestMethod]
    public async Task Membership_database_version_rejects_a_stale_tracked_write()
    {
        var (owner, workspace, _) = await SeedProjectAsync("member-xmin");
        var target = await SeedMemberAsync(owner.Id, workspace.Id, WorkspaceRole.Viewer);
        await using var first = CreateDbContext(CreateAccessor(owner.Id, workspace.Id));
        await using var second = CreateDbContext(CreateAccessor(owner.Id, workspace.Id));
        var firstRead = await first.WorkspaceMemberships.SingleAsync(row => row.UserId == target.UserId);
        var secondRead = await second.WorkspaceMemberships.SingleAsync(row => row.UserId == target.UserId);
        firstRead.ChangeRole(WorkspaceRole.ProjectContributor, DateTimeOffset.UtcNow);
        await first.SaveChangesAsync();
        secondRead.Deactivate(DateTimeOffset.UtcNow);
        await Assert.ThrowsExactlyAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync());
        var actual = await ReadMembershipAsync(target.UserId, workspace.Id);
        Assert.IsTrue(actual.IsActive);
        Assert.AreEqual(WorkspaceRole.ProjectContributor, actual.Role);
    }

    [TestMethod]
    public async Task Membership_mutations_recheck_workspace_suspension_despite_an_active_request_context()
    {
        var (owner, workspace, _) = await SeedProjectAsync("member-suspended");
        var target = await SeedMemberAsync(owner.Id, workspace.Id, WorkspaceRole.Viewer);
        await using (var context = CreateDbContext(CreateAccessor(owner.Id, workspace.Id)))
        {
            var persisted = await context.Workspaces.SingleAsync();
            persisted.Suspend(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }

        await using var provider = CreateServices(false);
        foreach (var operation in new[] { "invite", "change-role", "deactivate" })
        {
            await using var scope = provider.CreateAsyncScope();
            var service = MembershipService(scope.ServiceProvider, owner.Id, workspace.Id, WorkspaceRole.Owner);
            var failure = await CaptureAsync(() => operation switch
            {
                "invite" => (Task)service.InviteAsync($"integration|suspended-{Guid.NewGuid():N}", "Not Invited", "Viewer", CancellationToken.None),
                "change-role" => service.ChangeRoleAsync(target.UserId, "Owner", target.Version, CancellationToken.None),
                _ => service.DeactivateAsync(target.UserId, target.Version, CancellationToken.None),
            });
            Assert.IsInstanceOfType<MembershipManagementForbiddenException>(failure);
        }

        Assert.AreEqual(target, await ReadMembershipAsync(target.UserId, workspace.Id));
        await using var verified = CreateDbContext(CreateAccessor(owner.Id, workspace.Id));
        Assert.AreEqual(0, await verified.AuditEvents.CountAsync());
    }

    private static WorkspaceMembershipService MembershipService(IServiceProvider services, Guid userId, WorkspaceId workspaceId, WorkspaceRole role)
    {
        services.GetRequiredService<IWorkspaceContextAccessor>().Establish(new WorkspaceContext(userId, workspaceId, role, WorkspaceStatus.Active));
        return services.GetRequiredService<WorkspaceMembershipService>();
    }

    private static async Task<MemberSnapshot> SeedMemberAsync(Guid actorId, WorkspaceId workspaceId, WorkspaceRole role)
    {
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create($"integration|member-{Guid.NewGuid():N}", "Member", now);
        await using var context = CreateDbContext(CreateAccessor(actorId, workspaceId));
        var membership = WorkspaceMembership.Create(workspaceId, user.Id, role, now);
        context.Users.Add(user);
        context.WorkspaceMemberships.Add(membership);
        await context.SaveChangesAsync();
        return new MemberSnapshot(user.Id, role, true, MembershipVersion.Encode(membership.Version));
    }

    private static async Task<MemberSnapshot> ReadMembershipAsync(Guid userId, WorkspaceId workspaceId)
    {
        await using var context = CreateDbContext(CreateAccessor(userId, workspaceId));
        var member = await context.WorkspaceMemberships.SingleAsync(row => row.UserId == userId);
        return new MemberSnapshot(userId, member.Role, member.IsActive, MembershipVersion.Encode(member.Version));
    }

    private sealed record MemberSnapshot(Guid UserId, WorkspaceRole Role, bool IsActive, string Version);

    private sealed class MembershipRaceGate : DbCommandInterceptor
    {
        private readonly AsyncGate _gate = new(2);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FOR NO KEY UPDATE", StringComparison.Ordinal))
            {
                await _gate.SignalAndWaitAsync(cancellationToken);
            }

            return result;
        }
    }

    private sealed class PauseMembershipLock : DbCommandInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FOR NO KEY UPDATE", StringComparison.Ordinal))
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }

    private sealed class ObserveMembershipLock : DbCommandInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FOR NO KEY UPDATE", StringComparison.Ordinal))
            {
                Reached.TrySetResult();
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailAfterMembershipSave : SaveChangesInterceptor
    {
        public bool Injected { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            Injected = true;
            throw new IOException("Synthetic post-save failure");
        }
    }
}
