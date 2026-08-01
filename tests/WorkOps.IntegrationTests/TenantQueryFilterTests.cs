using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using WorkOps.Application;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Common;
using WorkOps.Application.Messaging;
using WorkOps.Application.Tenancy;
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

    [ClassInitialize]
    public static async Task InitializeAsync(TestContext _)
    {
        await Database.StartAsync();
        await RabbitMq.StartAsync();
        await using var dbContext = CreateDbContext(new WorkspaceContextAccessor());
        await dbContext.Database.MigrateAsync();
    }

    [ClassCleanup]
    public static async Task CleanupAsync()
    {
        await Database.DisposeAsync();
        await RabbitMq.DisposeAsync();
    }

    [TestMethod]
    public async Task Workspace_reads_are_default_deny_and_scoped_to_current_workspace()
    {
        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create("integration|tenant-filter", "Integration User", now);
        var first = Workspace.Create("First Workspace", $"first-{Guid.NewGuid():N}", now);
        var second = Workspace.Create("Second Workspace", $"second-{Guid.NewGuid():N}", now);

        await using (var seed = CreateDbContext(new WorkspaceContextAccessor()))
        {
            seed.Users.Add(user);
            seed.Workspaces.AddRange(first, second);
            seed.WorkspaceMemberships.AddRange(
                WorkspaceMembership.Create(first.Id, user.Id, WorkspaceRole.Owner, now),
                WorkspaceMembership.Create(second.Id, user.Id, WorkspaceRole.Viewer, now));
            await seed.SaveChangesAsync();
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

        await using (var seed = CreateDbContext(new WorkspaceContextAccessor()))
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

        await using (var seed = CreateDbContext(new WorkspaceContextAccessor()))
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

    private static WorkOpsDbContext CreateDbContext(WorkspaceContextAccessor accessor)
    {
        var options = new DbContextOptionsBuilder<WorkOpsDbContext>()
            .UseNpgsql(Database.GetConnectionString())
            .Options;

        return new WorkOpsDbContext(options, accessor);
    }

    private static ServiceProvider CreateServices(bool enableMessaging, Uri? rabbitUri = null)
    {
        var configurationValues = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:WorkOps"] = Database.GetConnectionString(),
            ["Messaging:Enabled"] = enableMessaging.ToString(),
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

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var services = new ServiceCollection();
        services.AddWorkOpsApplication();
        services.AddWorkOpsInfrastructure(configuration);
        return services.BuildServiceProvider();
    }
}
