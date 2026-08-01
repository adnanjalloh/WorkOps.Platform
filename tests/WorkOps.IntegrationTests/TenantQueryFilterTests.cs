using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Identity;
using WorkOps.Domain.Tenancy;
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

    private static WorkOpsDbContext CreateDbContext(WorkspaceContextAccessor accessor)
    {
        var options = new DbContextOptionsBuilder<WorkOpsDbContext>()
            .UseNpgsql(Database.GetConnectionString())
            .Options;

        return new WorkOpsDbContext(options, accessor);
    }
}
