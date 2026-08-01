using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WorkOps.Application.Common;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Identity;
using WorkOps.Domain.Projects;
using WorkOps.Domain.Tenancy;
using WorkOps.Domain.WorkItems;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.IntegrationTests;

[TestClass]
public sealed class TenantQueryFilterTests
{
    private static readonly PostgreSqlContainer Database = new PostgreSqlBuilder("postgres:18.4-alpine")
        .Build();

    [ClassInitialize]
    public static async Task InitializeAsync(TestContext _)
    {
        await Database.StartAsync();
        await using var dbContext = CreateDbContext(new WorkspaceContextAccessor());
        await dbContext.Database.MigrateAsync();
    }

    [ClassCleanup]
    public static async Task CleanupAsync()
    {
        await Database.DisposeAsync();
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
}
