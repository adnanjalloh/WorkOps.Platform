using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using WorkOps.Application;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Features;
using WorkOps.Application.Identity;
using WorkOps.Application.Messaging;
using WorkOps.Application.Projects;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Audit;
using WorkOps.Domain.Common;
using WorkOps.Domain.Features;
using WorkOps.Domain.Idempotency;
using WorkOps.Domain.Identity;
using WorkOps.Domain.Messaging;
using WorkOps.Domain.Projects;
using WorkOps.Domain.Tenancy;
using WorkOps.Domain.WorkItems;
using WorkOps.Infrastructure;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.IntegrationTests;

[TestClass]
public sealed class TenantQueryFilterTests
{
    private static readonly PostgreSqlContainer Database = new PostgreSqlBuilder("postgres:18.4-alpine")
        .Build();
    private static readonly RabbitMqContainer RabbitMq = new RabbitMqBuilder("rabbitmq:4.3.4-alpine")
        .Build();
    private static readonly RedisContainer Redis = new RedisBuilder("redis:8.8.1-alpine")
        .Build();

    [ClassInitialize]
    public static async Task InitializeAsync(TestContext _)
    {
        await Database.StartAsync();
        await RabbitMq.StartAsync();
        await Redis.StartAsync();
        await using var dbContext = CreateDbContext(new WorkspaceContextAccessor());
        await dbContext.Database.MigrateAsync();
    }

    [ClassCleanup]
    public static async Task CleanupAsync()
    {
        await Database.DisposeAsync();
        await RabbitMq.DisposeAsync();
        await Redis.DisposeAsync();
    }

    [TestMethod]
    public async Task Workspace_reads_are_default_deny_and_scoped_to_current_workspace()
    {
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create("integration|tenant-filter", "Integration User", now);
        var first = Workspace.Create("First Workspace", $"first-{Guid.NewGuid():N}", now);
        var second = Workspace.Create("Second Workspace", $"second-{Guid.NewGuid():N}", now);

        var seedAccessor = new WorkspaceContextAccessor();
        await using (var seed = CreateDbContext(seedAccessor))
        {
            seed.Users.Add(user);
            using (seedAccessor.BeginProvisioning(first.Id))
            {
                seed.Workspaces.Add(first);
                seed.WorkspaceSubscriptions.Add(WorkspaceSubscription.CreateStarter(first.Id, now));
                seed.WorkspaceMemberships.Add(
                    WorkspaceMembership.Create(first.Id, user.Id, WorkspaceRole.Owner, now));
                await seed.SaveChangesAsync();
            }

            using (seedAccessor.BeginProvisioning(second.Id))
            {
                seed.Workspaces.Add(second);
                seed.WorkspaceSubscriptions.Add(WorkspaceSubscription.CreateStarter(second.Id, now));
                seed.WorkspaceMemberships.Add(
                    WorkspaceMembership.Create(second.Id, user.Id, WorkspaceRole.Viewer, now));
                await seed.SaveChangesAsync();
            }
        }

        await using (var noContext = CreateDbContext(new WorkspaceContextAccessor()))
        {
            Assert.AreEqual(0, await noContext.Workspaces.CountAsync());
            Assert.AreEqual(0, await noContext.WorkspaceMemberships.CountAsync());
            Assert.AreEqual(0, await noContext.Projects.CountAsync());
            Assert.AreEqual(0, await noContext.WorkItems.CountAsync());
            Assert.AreEqual(0, await noContext.AuditEvents.CountAsync());
            Assert.AreEqual(0, await noContext.OutboxMessages.CountAsync());
            Assert.AreEqual(0, await noContext.InboxMessages.CountAsync());
            Assert.AreEqual(0, await noContext.NotificationDeliveries.CountAsync());
            Assert.AreEqual(0, await noContext.WorkspaceSubscriptions.CountAsync());
            Assert.AreEqual(0, await noContext.Attachments.CountAsync());
        }

        var firstAccessor = new WorkspaceContextAccessor();
        firstAccessor.Establish(new WorkspaceContext(
            user.Id,
            first.Id,
            WorkspaceRole.Owner,
            WorkspaceStatus.Active));

        await using (var firstContext = CreateDbContext(firstAccessor))
        {
            var visibleWorkspaces = await firstContext.Workspaces.AsNoTracking().ToArrayAsync();
            var visibleMemberships = await firstContext.WorkspaceMemberships.AsNoTracking().ToArrayAsync();

            Assert.HasCount(1, visibleWorkspaces);
            Assert.AreEqual(first.Id, visibleWorkspaces[0].Id);
            Assert.HasCount(1, visibleMemberships);
            Assert.AreEqual(first.Id, visibleMemberships[0].WorkspaceId);
            Assert.AreEqual(
                first.Id,
                (await firstContext.WorkspaceSubscriptions.AsNoTracking().SingleAsync()).WorkspaceId);
        }

        var secondAccessor = new WorkspaceContextAccessor();
        secondAccessor.Establish(new WorkspaceContext(
            user.Id,
            second.Id,
            WorkspaceRole.Viewer,
            WorkspaceStatus.Active));

        await using var secondContext = CreateDbContext(secondAccessor);
        var secondVisible = await secondContext.Workspaces.AsNoTracking().SingleAsync();

        Assert.AreEqual(second.Id, secondVisible.Id);
    }

    [TestMethod]
    public async Task Concurrent_work_item_updates_are_rejected_by_postgresql_version()
    {
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create(
            $"integration|concurrency-{Guid.NewGuid():N}",
            "Concurrency User",
            now);
        var workspace = Workspace.Create(
            "Concurrency Workspace",
            $"concurrency-{Guid.NewGuid():N}",
            now);
        var project = Project.Create(workspace.Id, "Concurrency Project", "concurrency-project", now);
        var workItem = WorkItem.Create(
            workspace.Id,
            project.Id,
            "Competing update",
            WorkItemPriority.Normal,
            user.Id,
            ["backend"],
            now);

        var seedAccessor = new WorkspaceContextAccessor();
        using (seedAccessor.BeginProvisioning(workspace.Id))
        await using (var seed = CreateDbContext(seedAccessor))
        {
            seed.Users.Add(user);
            seed.Workspaces.Add(workspace);
            seed.WorkspaceMemberships.Add(
                WorkspaceMembership.Create(workspace.Id, user.Id, WorkspaceRole.Owner, now));
            seed.Projects.Add(project);
            seed.WorkItems.Add(workItem);
            await seed.SaveChangesAsync();
        }

        var firstAccessor = CreateAccessor(user.Id, workspace.Id);
        var secondAccessor = CreateAccessor(user.Id, workspace.Id);
        await using var firstContext = CreateDbContext(firstAccessor);
        await using var secondContext = CreateDbContext(secondAccessor);
        var firstCopy = await firstContext.WorkItems.SingleAsync(item => item.Id == workItem.Id);
        var secondCopy = await secondContext.WorkItems.SingleAsync(item => item.Id == workItem.Id);

        firstCopy.UpdateDetails(
            "First update",
            WorkItemPriority.High,
            user.Id,
            ["backend"],
            now.AddMinutes(1));
        secondCopy.UpdateDetails(
            "Second update",
            WorkItemPriority.Critical,
            user.Id,
            ["backend"],
            now.AddMinutes(2));

        await firstContext.SaveChangesAsync();
        await Assert.ThrowsExactlyAsync<ConcurrencyConflictException>(
            () => secondContext.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Concurrent_workers_lease_an_outbox_message_once()
    {
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create(
            $"integration|outbox-{Guid.NewGuid():N}",
            "Outbox User",
            now);
        var workspace = Workspace.Create(
            "Outbox Workspace",
            $"outbox-{Guid.NewGuid():N}",
            now);
        var message = OutboxMessage.Create(
            Guid.NewGuid(),
            workspace.Id,
            WorkItemStatusChangedMessage.MessageType,
            "{}",
            now);

        var seedAccessor = new WorkspaceContextAccessor();
        using (seedAccessor.BeginProvisioning(workspace.Id))
        await using (var seed = CreateDbContext(seedAccessor))
        {
            seed.Users.Add(user);
            seed.Workspaces.Add(workspace);
            seed.WorkspaceMemberships.Add(
                WorkspaceMembership.Create(workspace.Id, user.Id, WorkspaceRole.Owner, now));
            seed.OutboxMessages.Add(message);
            await seed.SaveChangesAsync();
        }

        await using var provider = CreateServices(enableMessaging: false);
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstStore = firstScope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var secondStore = secondScope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var leases = await Task.WhenAll(
            firstStore.LeaseNextAsync(now, now.AddSeconds(30), CancellationToken.None),
            secondStore.LeaseNextAsync(now, now.AddSeconds(30), CancellationToken.None));

        Assert.HasCount(1, leases.Where(static lease => lease is not null));
        Assert.AreEqual(1, leases.Single(static lease => lease is not null)!.AttemptCount);
    }

    [TestMethod]
    public async Task Notification_deduplication_swallows_only_expected_constraints()
    {
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N");
        var user = ApplicationUser.Create(
            $"integration|notification-{suffix}",
            "Notification User",
            now);
        var workspace = Workspace.Create(
            "Notification Workspace",
            $"notification-{suffix}",
            now);
        var firstMessageId = Guid.NewGuid();
        var secondMessageId = Guid.NewGuid();
        var seedAccessor = new WorkspaceContextAccessor();
        using (seedAccessor.BeginProvisioning(workspace.Id))
        await using (var seed = CreateDbContext(seedAccessor))
        {
            seed.Users.Add(user);
            seed.Workspaces.Add(workspace);
            seed.WorkspaceMemberships.Add(
                WorkspaceMembership.Create(workspace.Id, user.Id, WorkspaceRole.Owner, now));
            seed.OutboxMessages.AddRange(
                OutboxMessage.Create(
                    firstMessageId,
                    workspace.Id,
                    WorkItemStatusChangedMessage.MessageType,
                    "{}",
                    now),
                OutboxMessage.Create(
                    secondMessageId,
                    workspace.Id,
                    WorkItemStatusChangedMessage.MessageType,
                    "{}",
                    now));
            await seed.SaveChangesAsync();
        }

        var firstMessage = CreateNotificationMessage(firstMessageId, workspace.Id, user.Id, now);
        await using var provider = CreateServices(enableMessaging: false);
        await using (var firstScope = provider.CreateAsyncScope())
        {
            var accessor = firstScope.ServiceProvider.GetRequiredService<IWorkspaceContextAccessor>();
            accessor.EstablishBackground(workspace.Id);
            var store = firstScope.ServiceProvider.GetRequiredService<INotificationStore>();
            Assert.IsTrue(await store.TryDeliverAsync(firstMessage, now, CancellationToken.None));
        }

        await using (var duplicateScope = provider.CreateAsyncScope())
        {
            var accessor = duplicateScope.ServiceProvider.GetRequiredService<IWorkspaceContextAccessor>();
            accessor.EstablishBackground(workspace.Id);
            var store = duplicateScope.ServiceProvider.GetRequiredService<INotificationStore>();
            Assert.IsFalse(await store.TryDeliverAsync(firstMessage, now, CancellationToken.None));
        }

        await using (var unrelatedScope = provider.CreateAsyncScope())
        {
            var accessor = unrelatedScope.ServiceProvider.GetRequiredService<IWorkspaceContextAccessor>();
            accessor.EstablishBackground(workspace.Id);
            var dbContext = unrelatedScope.ServiceProvider.GetRequiredService<WorkOpsDbContext>();
            dbContext.Users.Add(ApplicationUser.Create(user.Subject, "Duplicate Subject", now));
            var store = unrelatedScope.ServiceProvider.GetRequiredService<INotificationStore>();
            var secondMessage = CreateNotificationMessage(
                secondMessageId,
                workspace.Id,
                user.Id,
                now);

            await Assert.ThrowsExactlyAsync<DuplicateIdentitySubjectException>(
                () => store.TryDeliverAsync(secondMessage, now, CancellationToken.None));
        }
    }

    [TestMethod]
    public async Task Rabbitmq_adapter_publishes_a_persistent_routable_message()
    {
        var rabbitUri = new Uri(RabbitMq.GetConnectionString());
        await using var provider = CreateServices(enableMessaging: true, rabbitUri);
        var publisher = provider.GetRequiredService<IMessagePublisher>();
        var message = new OutboxLease(
            Guid.NewGuid(),
            WorkOps.Domain.WorkspaceId.New(),
            WorkItemStatusChangedMessage.MessageType,
            "{\"safe\":true}",
            1,
            DateTimeOffset.UtcNow);

        await publisher.PublishAsync(message, CancellationToken.None);

        var factory = new ConnectionFactory { Uri = rabbitUri };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var delivery = await channel.BasicGetAsync(
            "workops.notifications.v1",
            autoAck: true,
            CancellationToken.None);

        Assert.IsNotNull(delivery);
        Assert.AreEqual(message.Id.ToString("D"), delivery.BasicProperties.MessageId);
        Assert.AreEqual(DeliveryModes.Persistent, delivery.BasicProperties.DeliveryMode);
        Assert.AreEqual(message.PayloadJson, Encoding.UTF8.GetString(delivery.Body.Span));
    }

    [TestMethod]
    public async Task Redis_feature_cache_is_tenant_scoped_and_invalidated()
    {
        await using var provider = CreateServices(
            enableMessaging: false,
            enableCache: true,
            redisConnectionString: Redis.GetConnectionString());
        var cache = provider.GetRequiredService<IFeatureCache>();
        var firstWorkspace = WorkOps.Domain.WorkspaceId.New();
        var secondWorkspace = WorkOps.Domain.WorkspaceId.New();
        var firstFactoryCalls = 0;
        var secondFactoryCalls = 0;

        Task<FeatureSnapshot> FirstFactory(CancellationToken _) => Task.FromResult(
            new FeatureSnapshot("Starter", 2, ++firstFactoryCalls));
        Task<FeatureSnapshot> SecondFactory(CancellationToken _) => Task.FromResult(
            new FeatureSnapshot("Team", 20, ++secondFactoryCalls));

        var first = await cache.GetOrCreateAsync(
            firstWorkspace,
            FirstFactory,
            CancellationToken.None);
        var firstCached = await cache.GetOrCreateAsync(
            firstWorkspace,
            FirstFactory,
            CancellationToken.None);
        var second = await cache.GetOrCreateAsync(
            secondWorkspace,
            SecondFactory,
            CancellationToken.None);

        Assert.AreEqual("Starter", first.Plan);
        Assert.AreEqual(first, firstCached);
        Assert.AreEqual("Team", second.Plan);
        Assert.AreEqual(1, firstFactoryCalls);
        Assert.AreEqual(1, secondFactoryCalls);

        await cache.InvalidateAsync(firstWorkspace, CancellationToken.None);
        var refreshed = await cache.GetOrCreateAsync(
            firstWorkspace,
            FirstFactory,
            CancellationToken.None);

        Assert.AreEqual(2, refreshed.ActiveProjectCount);
        Assert.AreEqual(2, firstFactoryCalls);
        Assert.AreEqual(1, secondFactoryCalls);
    }

    [TestMethod]
    public async Task Redis_feature_cache_recovers_from_malformed_and_incompatible_values()
    {
        await using var provider = CreateServices(
            enableMessaging: false,
            enableCache: true,
            redisConnectionString: Redis.GetConnectionString());
        var cache = provider.GetRequiredService<IFeatureCache>();
        var connection = provider.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
        var database = connection.GetDatabase();
        var workspaceId = WorkOps.Domain.WorkspaceId.New();
        var cacheKey = $"workops:{workspaceId.Value:N}:features";
        var corruptValues = new[]
        {
            "{not-json",
            "{\"plan\":\"Starter\"}",
        };
        var factoryCalls = 0;

        foreach (var corruptValue in corruptValues)
        {
            await database.StringSetAsync(cacheKey, corruptValue);
            var result = await cache.GetOrCreateAsync(
                workspaceId,
                _ => Task.FromResult(new FeatureSnapshot("Starter", 2, ++factoryCalls)),
                CancellationToken.None);

            Assert.AreEqual("Starter", result.Plan);
            Assert.IsFalse(await database.KeyExistsAsync(cacheKey));
        }

        Assert.AreEqual(2, factoryCalls);
    }

    [TestMethod]
    public async Task Concurrent_project_reservations_cannot_bypass_the_plan_limit()
    {
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create(
            $"integration|quota-{Guid.NewGuid():N}",
            "Quota User",
            now);
        var workspace = Workspace.Create(
            "Quota Workspace",
            $"quota-{Guid.NewGuid():N}",
            now);
        var subscription = WorkspaceSubscription.CreateStarter(workspace.Id, now);
        subscription.ReserveProjectSlot(2, now);

        var seedAccessor = new WorkspaceContextAccessor();
        using (seedAccessor.BeginProvisioning(workspace.Id))
        await using (var seed = CreateDbContext(seedAccessor))
        {
            seed.Users.Add(user);
            seed.Workspaces.Add(workspace);
            seed.WorkspaceMemberships.Add(
                WorkspaceMembership.Create(workspace.Id, user.Id, WorkspaceRole.Owner, now));
            seed.WorkspaceSubscriptions.Add(subscription);
            await seed.SaveChangesAsync();
        }

        await using var firstContext = CreateDbContext(CreateAccessor(user.Id, workspace.Id));
        await using var secondContext = CreateDbContext(CreateAccessor(user.Id, workspace.Id));
        var firstCopy = await firstContext.WorkspaceSubscriptions.SingleAsync();
        var secondCopy = await secondContext.WorkspaceSubscriptions.SingleAsync();
        firstCopy.ReserveProjectSlot(2, now.AddSeconds(1));
        secondCopy.ReserveProjectSlot(2, now.AddSeconds(2));

        await firstContext.SaveChangesAsync();
        await Assert.ThrowsExactlyAsync<ConcurrencyConflictException>(
            () => secondContext.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Tenant_insert_without_a_workspace_context_is_rejected()
    {
        await using var context = CreateDbContext(new WorkspaceContextAccessor());
        context.Projects.Add(Project.Create(
            WorkOps.Domain.WorkspaceId.New(),
            "Unscoped Project",
            $"unscoped-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow));

        await Assert.ThrowsExactlyAsync<TenantWriteBoundaryException>(
            () => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Cross_workspace_insert_is_rejected()
    {
        var allowedWorkspaceId = WorkOps.Domain.WorkspaceId.New();
        await using var context = CreateDbContext(CreateAccessor(Guid.NewGuid(), allowedWorkspaceId));
        context.Projects.Add(Project.Create(
            WorkOps.Domain.WorkspaceId.New(),
            "Foreign Project",
            $"foreign-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow));

        await Assert.ThrowsExactlyAsync<TenantWriteBoundaryException>(
            () => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Cross_workspace_update_is_rejected()
    {
        var seeded = await SeedProjectAsync("update-boundary");
        await using var context = CreateDbContext(
            CreateAccessor(seeded.User.Id, WorkOps.Domain.WorkspaceId.New()));
        var project = await context.Projects
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == seeded.Project.Id);
        project.Archive(DateTimeOffset.UtcNow);

        await Assert.ThrowsExactlyAsync<TenantWriteBoundaryException>(
            () => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Cross_workspace_delete_is_rejected()
    {
        var seeded = await SeedProjectAsync("delete-boundary");
        await using var context = CreateDbContext(
            CreateAccessor(seeded.User.Id, WorkOps.Domain.WorkspaceId.New()));
        var project = await context.Projects
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == seeded.Project.Id);
        context.Projects.Remove(project);

        await Assert.ThrowsExactlyAsync<TenantWriteBoundaryException>(
            () => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Workspace_id_mutation_is_rejected()
    {
        var seeded = await SeedProjectAsync("reparent-boundary");
        var auditEvent = AuditEvent.Record(
            seeded.Workspace.Id,
            seeded.User.Id,
            "boundary.tested",
            "project",
            seeded.Project.Id,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"),
            "{}");
        await using (var seed = CreateDbContext(CreateAccessor(seeded.User.Id, seeded.Workspace.Id)))
        {
            seed.AuditEvents.Add(auditEvent);
            await seed.SaveChangesAsync();
        }

        await using var context = CreateDbContext(CreateAccessor(seeded.User.Id, seeded.Workspace.Id));
        var loaded = await context.AuditEvents.SingleAsync(candidate => candidate.Id == auditEvent.Id);
        context.Entry(loaded)
            .Property(nameof(IWorkspaceOwned.WorkspaceId))
            .CurrentValue = WorkOps.Domain.WorkspaceId.New();

        await Assert.ThrowsExactlyAsync<TenantWriteBoundaryException>(
            () => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Workspace_root_write_without_a_context_is_rejected()
    {
        var seeded = await SeedProjectAsync("root-no-context");
        await using var context = CreateDbContext(new WorkspaceContextAccessor());
        var workspace = await context.Workspaces
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == seeded.Workspace.Id);
        workspace.Suspend(DateTimeOffset.UtcNow);

        await Assert.ThrowsExactlyAsync<TenantWriteBoundaryException>(
            () => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Cross_workspace_root_update_and_delete_are_rejected()
    {
        var seeded = await SeedProjectAsync("root-cross-tenant");
        var foreignWorkspaceId = WorkOps.Domain.WorkspaceId.New();

        await using (var update = CreateDbContext(CreateAccessor(seeded.User.Id, foreignWorkspaceId)))
        {
            var workspace = await update.Workspaces
                .IgnoreQueryFilters()
                .SingleAsync(candidate => candidate.Id == seeded.Workspace.Id);
            workspace.Suspend(DateTimeOffset.UtcNow);

            await Assert.ThrowsExactlyAsync<TenantWriteBoundaryException>(
                () => update.SaveChangesAsync());
        }

        await using (var delete = CreateDbContext(CreateAccessor(seeded.User.Id, foreignWorkspaceId)))
        {
            var workspace = await delete.Workspaces
                .IgnoreQueryFilters()
                .SingleAsync(candidate => candidate.Id == seeded.Workspace.Id);
            delete.Workspaces.Remove(workspace);

            await Assert.ThrowsExactlyAsync<TenantWriteBoundaryException>(
                () => delete.SaveChangesAsync());
        }
    }

    [TestMethod]
    public async Task Attached_foreign_workspace_root_is_rejected()
    {
        var seeded = await SeedProjectAsync("root-attached");
        await using var context = CreateDbContext(
            CreateAccessor(seeded.User.Id, WorkOps.Domain.WorkspaceId.New()));
        context.Attach(seeded.Workspace);
        seeded.Workspace.Suspend(DateTimeOffset.UtcNow);

        await Assert.ThrowsExactlyAsync<TenantWriteBoundaryException>(
            () => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Workspace_root_identifier_mutation_fails_before_persistence()
    {
        var seeded = await SeedProjectAsync("root-id-mutation");
        var replacementId = WorkOps.Domain.WorkspaceId.New();
        await using (var context = CreateDbContext(
                         CreateAccessor(seeded.User.Id, seeded.Workspace.Id)))
        {
            var workspace = await context.Workspaces.SingleAsync();
            var idField = typeof(Workspace).GetField(
                "<Id>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(idField);
            idField.SetValue(workspace, replacementId);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.SaveChangesAsync());
        }

        await using var verify = CreateDbContext(new WorkspaceContextAccessor());
        Assert.IsTrue(await verify.Workspaces
            .IgnoreQueryFilters()
            .AnyAsync(candidate => candidate.Id == seeded.Workspace.Id));
        Assert.IsFalse(await verify.Workspaces
            .IgnoreQueryFilters()
            .AnyAsync(candidate => candidate.Id == replacementId));
    }

    [TestMethod]
    public async Task Workspace_root_creation_requires_a_matching_provisioning_scope()
    {
        var workspace = Workspace.Create(
            "Provisioning Boundary",
            $"provisioning-boundary-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);
        var accessor = new WorkspaceContextAccessor();
        using (accessor.BeginProvisioning(WorkOps.Domain.WorkspaceId.New()))
        await using (var rejected = CreateDbContext(accessor))
        {
            rejected.Workspaces.Add(workspace);
            await Assert.ThrowsExactlyAsync<TenantWriteBoundaryException>(
                () => rejected.SaveChangesAsync());
        }

        var acceptedAccessor = new WorkspaceContextAccessor();
        using (acceptedAccessor.BeginProvisioning(workspace.Id))
        await using (var accepted = CreateDbContext(acceptedAccessor))
        {
            accepted.Workspaces.Add(workspace);
            await accepted.SaveChangesAsync();
        }
    }

    [TestMethod]
    public async Task Interactive_and_background_writes_accept_the_matching_workspace()
    {
        var seeded = await SeedProjectAsync("valid-boundary");

        await using (var interactive = CreateDbContext(
                         CreateAccessor(seeded.User.Id, seeded.Workspace.Id)))
        {
            interactive.Projects.Add(Project.Create(
                seeded.Workspace.Id,
                "Interactive Project",
                $"interactive-{Guid.NewGuid():N}",
                DateTimeOffset.UtcNow));
            await interactive.SaveChangesAsync();
        }

        var backgroundAccessor = new WorkspaceContextAccessor();
        backgroundAccessor.EstablishBackground(seeded.Workspace.Id);
        await using (var background = CreateDbContext(backgroundAccessor))
        {
            background.Projects.Add(Project.Create(
                seeded.Workspace.Id,
                "Background Project",
                $"background-{Guid.NewGuid():N}",
                DateTimeOffset.UtcNow));
            await background.SaveChangesAsync();
        }

        await using var verify = CreateDbContext(CreateAccessor(seeded.User.Id, seeded.Workspace.Id));
        Assert.AreEqual(3, await verify.Projects.CountAsync());
    }

    [TestMethod]
    public async Task Concurrent_identity_materialization_returns_one_exact_subject_row()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var identity = new CurrentIdentity($"integration|identity-race-{suffix}", "Identity Race");
        await using var provider = CreateServices(enableMessaging: false);
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstService = firstScope.ServiceProvider.GetRequiredService<IdentityService>();
        var secondService = secondScope.ServiceProvider.GetRequiredService<IdentityService>();

        var users = await Task.WhenAll(
            firstService.GetOrCreateAsync(identity, CancellationToken.None),
            secondService.GetOrCreateAsync(identity, CancellationToken.None));

        Assert.AreEqual(users[0].Id, users[1].Id);
        await using var verify = CreateDbContext(new WorkspaceContextAccessor());
        Assert.AreEqual(
            1,
            await verify.Users.CountAsync(user => user.Subject == identity.Subject));
    }

    [TestMethod]
    public async Task Concurrent_workspace_slug_race_rolls_back_the_losing_identity_and_graph()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var slug = $"workspace-race-{suffix}";
        var identities = new[]
        {
            new CurrentIdentity($"integration|workspace-race-a-{suffix}", "Race A"),
            new CurrentIdentity($"integration|workspace-race-b-{suffix}", "Race B"),
        };
        var gate = new AsyncGate(participantCount: 2);
        await using var provider = CreateServices(enableMessaging: false);
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstService = CreateWorkspaceService(firstScope.ServiceProvider, gate);
        var secondService = CreateWorkspaceService(secondScope.ServiceProvider, gate);

        var results = await Task.WhenAll(
            CaptureAsync(() => firstService.CreateAsync(
                identities[0],
                "Workspace Race A",
                slug,
                CancellationToken.None)),
            CaptureAsync(() => secondService.CreateAsync(
                identities[1],
                "Workspace Race B",
                slug,
                CancellationToken.None)));

        Assert.AreEqual(1, results.Count(static exception => exception is null));
        Assert.AreEqual(
            1,
            results.Count(static exception => exception is DuplicateWorkspaceSlugException));

        await using var verify = CreateDbContext(new WorkspaceContextAccessor());
        var workspaceId = await verify.Workspaces
            .IgnoreQueryFilters()
            .Where(workspace => workspace.Slug == slug)
            .Select(workspace => workspace.Id)
            .SingleAsync();
        var subjects = identities.Select(static identity => identity.Subject).ToArray();
        Assert.AreEqual(1, await verify.Users.CountAsync(user => subjects.Contains(user.Subject)));
        Assert.AreEqual(
            1,
            await verify.WorkspaceMemberships
                .IgnoreQueryFilters()
                .CountAsync(membership => membership.WorkspaceId == workspaceId));
        Assert.AreEqual(
            1,
            await verify.WorkspaceSubscriptions
                .IgnoreQueryFilters()
                .CountAsync(subscription => subscription.WorkspaceId == workspaceId));
        Assert.AreEqual(
            1,
            await verify.AuditEvents
                .IgnoreQueryFilters()
                .CountAsync(auditEvent => auditEvent.WorkspaceId == workspaceId));
    }

    [TestMethod]
    public async Task Concurrent_membership_and_project_keys_map_to_exact_conflicts()
    {
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N");
        var owner = ApplicationUser.Create(
            $"integration|owner-{suffix}",
            "Race Owner",
            now);
        var member = ApplicationUser.Create(
            $"integration|member-{suffix}",
            "Race Member",
            now);
        var workspace = Workspace.Create("Constraint Workspace", $"constraint-{suffix}", now);
        var seedAccessor = new WorkspaceContextAccessor();
        using (seedAccessor.BeginProvisioning(workspace.Id))
        await using (var seed = CreateDbContext(seedAccessor))
        {
            seed.Users.AddRange(owner, member);
            seed.Workspaces.Add(workspace);
            seed.WorkspaceMemberships.Add(
                WorkspaceMembership.Create(workspace.Id, owner.Id, WorkspaceRole.Owner, now));
            await seed.SaveChangesAsync();
        }

        await using (var firstMembership = CreateDbContext(CreateAccessor(owner.Id, workspace.Id)))
        await using (var secondMembership = CreateDbContext(CreateAccessor(owner.Id, workspace.Id)))
        {
            firstMembership.WorkspaceMemberships.Add(
                WorkspaceMembership.Create(workspace.Id, member.Id, WorkspaceRole.Viewer, now));
            secondMembership.WorkspaceMemberships.Add(
                WorkspaceMembership.Create(workspace.Id, member.Id, WorkspaceRole.Viewer, now));
            var membershipResults = await Task.WhenAll(
                CaptureAsync(() => firstMembership.SaveChangesAsync()),
                CaptureAsync(() => secondMembership.SaveChangesAsync()));

            Assert.AreEqual(1, membershipResults.Count(static exception => exception is null));
            Assert.AreEqual(
                1,
                membershipResults.Count(
                    static exception => exception is DuplicateWorkspaceMembershipException));
        }

        await using var firstProject = CreateDbContext(CreateAccessor(owner.Id, workspace.Id));
        await using var secondProject = CreateDbContext(CreateAccessor(owner.Id, workspace.Id));
        var projectKey = $"same-key-{suffix}";
        firstProject.Projects.Add(Project.Create(workspace.Id, "First", projectKey, now));
        secondProject.Projects.Add(Project.Create(workspace.Id, "Second", projectKey, now));
        var projectResults = await Task.WhenAll(
            CaptureAsync(() => firstProject.SaveChangesAsync()),
            CaptureAsync(() => secondProject.SaveChangesAsync()));

        Assert.AreEqual(1, projectResults.Count(static exception => exception is null));
        Assert.AreEqual(
            1,
            projectResults.Count(static exception => exception is DuplicateProjectKeyException));
    }

    [TestMethod]
    public async Task Idempotency_retention_purges_expired_rows_in_cross_tenant_batches()
    {
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N");
        var user = ApplicationUser.Create(
            $"integration|retention-{suffix}",
            "Retention User",
            now);
        var first = Workspace.Create("Retention First", $"retention-first-{suffix}", now);
        var second = Workspace.Create("Retention Second", $"retention-second-{suffix}", now);
        var seedAccessor = new WorkspaceContextAccessor();
        await using (var seed = CreateDbContext(seedAccessor))
        {
            seed.Users.Add(user);
            using (seedAccessor.BeginProvisioning(first.Id))
            {
                seed.Workspaces.Add(first);
                seed.WorkspaceMemberships.Add(
                    WorkspaceMembership.Create(first.Id, user.Id, WorkspaceRole.Owner, now));
                seed.IdempotencyRecords.AddRange(
                    CreateIdempotencyRecord(first.Id, user.Id, $"first-a-{suffix}", now.AddMinutes(-2)),
                    CreateIdempotencyRecord(first.Id, user.Id, $"first-b-{suffix}", now.AddMinutes(-1)),
                    CreateIdempotencyRecord(first.Id, user.Id, $"current-{suffix}", now.AddHours(1)));
                await seed.SaveChangesAsync();
            }

            using (seedAccessor.BeginProvisioning(second.Id))
            {
                seed.Workspaces.Add(second);
                seed.WorkspaceMemberships.Add(
                    WorkspaceMembership.Create(second.Id, user.Id, WorkspaceRole.Owner, now));
                seed.IdempotencyRecords.Add(
                    CreateIdempotencyRecord(second.Id, user.Id, $"second-{suffix}", now.AddMinutes(-1)));
                await seed.SaveChangesAsync();
            }
        }

        await using var provider = CreateServices(enableMessaging: false);
        await using var scope = provider.CreateAsyncScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<IIdempotencyMaintenanceStore>();
        var firstBatch = await maintenance.PurgeExpiredBatchAsync(now, 2, CancellationToken.None);
        var secondBatch = await maintenance.PurgeExpiredBatchAsync(now, 2, CancellationToken.None);

        Assert.AreEqual(2, firstBatch);
        Assert.AreEqual(1, secondBatch);
        await using var verify = CreateDbContext(new WorkspaceContextAccessor());
        Assert.AreEqual(
            1,
            await verify.IdempotencyRecords
                .IgnoreQueryFilters()
                .CountAsync(record => record.UserId == user.Id));
    }

    [TestMethod]
    public async Task Local_file_storage_uses_separate_tenant_directories()
    {
        var fileRoot = Path.Combine(
            Path.GetTempPath(),
            $"workops-storage-test-{Guid.NewGuid():N}");
        try
        {
            await using var provider = CreateServices(
                enableMessaging: false,
                fileRoot: fileRoot);
            var storage = provider.GetRequiredService<IFileStorage>();
            var firstWorkspace = WorkOps.Domain.WorkspaceId.New();
            var secondWorkspace = WorkOps.Domain.WorkspaceId.New();
            const string storageName = "0123456789abcdef0123456789abcdef.bin";

            await storage.SaveAsync(
                firstWorkspace,
                storageName,
                "first"u8.ToArray(),
                CancellationToken.None);
            await storage.SaveAsync(
                secondWorkspace,
                storageName,
                "second"u8.ToArray(),
                CancellationToken.None);

            await using var first = await storage.OpenReadAsync(
                firstWorkspace,
                storageName,
                CancellationToken.None);
            await using var second = await storage.OpenReadAsync(
                secondWorkspace,
                storageName,
                CancellationToken.None);
            using var firstReader = new StreamReader(first);
            using var secondReader = new StreamReader(second);

            Assert.AreEqual("first", await firstReader.ReadToEndAsync());
            Assert.AreEqual("second", await secondReader.ReadToEndAsync());
        }
        finally
        {
            if (Directory.Exists(fileRoot))
            {
                Directory.Delete(fileRoot, recursive: true);
            }
        }
    }

    private static WorkspaceContextAccessor CreateAccessor(Guid userId, WorkOps.Domain.WorkspaceId workspaceId)
    {
        var accessor = new WorkspaceContextAccessor();
        accessor.Establish(new WorkspaceContext(
            userId,
            workspaceId,
            WorkspaceRole.Owner,
            WorkspaceStatus.Active));
        return accessor;
    }

    private static async Task<(ApplicationUser User, Workspace Workspace, Project Project)> SeedProjectAsync(
        string purpose)
    {
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N");
        var user = ApplicationUser.Create(
            $"integration|{purpose}-{suffix}",
            "Boundary User",
            now);
        var workspace = Workspace.Create(
            "Boundary Workspace",
            $"{purpose}-{suffix}",
            now);
        var project = Project.Create(
            workspace.Id,
            "Boundary Project",
            $"boundary-{suffix}",
            now);
        var accessor = new WorkspaceContextAccessor();

        using (accessor.BeginProvisioning(workspace.Id))
        await using (var context = CreateDbContext(accessor))
        {
            context.Users.Add(user);
            context.Workspaces.Add(workspace);
            context.WorkspaceMemberships.Add(
                WorkspaceMembership.Create(workspace.Id, user.Id, WorkspaceRole.Owner, now));
            context.Projects.Add(project);
            await context.SaveChangesAsync();
        }

        return (user, workspace, project);
    }

    private static WorkItemStatusChangedMessage CreateNotificationMessage(
        Guid messageId,
        WorkOps.Domain.WorkspaceId workspaceId,
        Guid userId,
        DateTimeOffset occurredAt) => new(
        messageId,
        workspaceId.Value,
        userId,
        userId,
        Guid.NewGuid(),
        "Backlog",
        "InProgress",
        occurredAt,
        "integration-test");

    private static IdempotencyRecord CreateIdempotencyRecord(
        WorkOps.Domain.WorkspaceId workspaceId,
        Guid userId,
        string key,
        DateTimeOffset expiresAt) => IdempotencyRecord.Create(
        workspaceId,
        userId,
        "POST",
        "/api/v1/projects",
        key,
        new string('A', 64),
        201,
        "{}",
        expiresAt.AddHours(-24),
        expiresAt);

    private static WorkOpsDbContext CreateDbContext(WorkspaceContextAccessor accessor)
    {
        var options = new DbContextOptionsBuilder<WorkOpsDbContext>()
            .UseNpgsql(Database.GetConnectionString())
            .Options;

        return new WorkOpsDbContext(options, accessor);
    }

    private static WorkspaceService CreateWorkspaceService(
        IServiceProvider services,
        AsyncGate gate) => new(
        services.GetRequiredService<IdentityService>(),
        new CoordinatedWorkspaceStore(services.GetRequiredService<IWorkspaceStore>(), gate),
        services.GetRequiredService<IWorkspaceSubscriptionStore>(),
        services.GetRequiredService<IUnitOfWork>(),
        services.GetRequiredService<AuditWriter>(),
        services.GetRequiredService<IWorkspaceContextAccessor>(),
        services.GetRequiredService<IInputSanitizer>(),
        services.GetRequiredService<TimeProvider>());

    private static async Task<Exception?> CaptureAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static ServiceProvider CreateServices(
        bool enableMessaging,
        Uri? rabbitUri = null,
        bool enableCache = false,
        string? redisConnectionString = null,
        string? fileRoot = null)
    {
        var configurationValues = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:WorkOps"] = Database.GetConnectionString(),
            ["Messaging:Enabled"] = enableMessaging.ToString(),
            ["Cache:Enabled"] = enableCache.ToString(),
            ["Files:RootPath"] = fileRoot ?? Path.Combine(
                Path.GetTempPath(),
                "workops-integration-default"),
        };
        if (enableMessaging && rabbitUri is not null)
        {
            var credentials = rabbitUri.UserInfo.Split(':', 2);
            configurationValues["Messaging:HostName"] = rabbitUri.Host;
            configurationValues["Messaging:Port"] = rabbitUri.Port.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            configurationValues["Messaging:VirtualHost"] = "/";
            configurationValues["Messaging:UserName"] = Uri.UnescapeDataString(credentials[0]);
            configurationValues["Messaging:Password"] = Uri.UnescapeDataString(credentials[1]);
        }

        if (enableCache)
        {
            configurationValues["ConnectionStrings:Redis"] = redisConnectionString;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var services = new ServiceCollection();
        services.AddWorkOpsApplication();
        services.AddWorkOpsInfrastructure(configuration);
        services.AddSingleton<ICorrelationContext>(new TestCorrelationContext());
        return services.BuildServiceProvider();
    }

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public string CorrelationId => "integration-test";
    }

    private sealed class AsyncGate(int participantCount)
    {
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public async Task SignalAndWaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == participantCount)
            {
                _release.SetResult();
            }

            await _release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class CoordinatedWorkspaceStore(
        IWorkspaceStore inner,
        AsyncGate gate) : IWorkspaceStore
    {
        public async Task<bool> SlugExistsAsync(
            string slug,
            CancellationToken cancellationToken)
        {
            var exists = await inner.SlugExistsAsync(slug, cancellationToken);
            await gate.SignalAndWaitAsync(cancellationToken);
            return exists;
        }

        public void Add(Workspace workspace) => inner.Add(workspace);

        public void Add(WorkspaceMembership membership) => inner.Add(membership);

        public Task<WorkspaceMembership?> FindCurrentMembershipAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            inner.FindCurrentMembershipAsync(userId, cancellationToken);

        public Task<bool> IsCurrentMemberActiveAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            inner.IsCurrentMemberActiveAsync(userId, cancellationToken);

        public Task<Workspace?> GetCurrentAsync(CancellationToken cancellationToken) =>
            inner.GetCurrentAsync(cancellationToken);

        public Task<IReadOnlyList<WorkspaceMemberView>> ListCurrentMembersAsync(
            CancellationToken cancellationToken) =>
            inner.ListCurrentMembersAsync(cancellationToken);
    }
}
