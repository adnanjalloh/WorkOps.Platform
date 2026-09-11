using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using WorkOps.Application.Tenancy;
using WorkOps.Contracts.Common;
using WorkOps.Contracts.Tenancy;
using WorkOps.Contracts.WorkItems;
using WorkOps.Domain;
using WorkOps.Domain.Tenancy;
using WorkOps.Domain.WorkItems;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.FunctionalTests;

public sealed partial class TenantIdentityEndpointTests
{
    [TestMethod]
    public async Task Work_item_listing_has_stable_pages_and_composable_literal_filters()
    {
        using var owner = CreateAuthorizedClient($"functional|list-{Guid.NewGuid():N}", "List Owner");
        var (workspace, items) = await SeedWorkItemListAsync(owner);
        var expected = items.OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id).Select(item => item.Id).ToArray();

        var all = await ListWorkItemsAsync(owner, workspace.Id, string.Empty);
        Assert.AreEqual(1, all.Page);
        Assert.AreEqual(20, all.PageSize);
        Assert.AreEqual(4, all.TotalCount);
        CollectionAssert.AreEqual(expected, all.Items.Select(item => item.Id).ToArray());
        Assert.IsTrue(all.Items.All(item => !string.IsNullOrEmpty(item.Version)));
        Assert.IsTrue(all.Items.Where(item => item.AssigneeUserId.HasValue)
            .All(item => !string.IsNullOrEmpty(item.AssigneeDisplayName)));

        var first = await ListWorkItemsAsync(owner, workspace.Id, "?pageSize=2");
        var second = await ListWorkItemsAsync(owner, workspace.Id, "?pageSize=2&page=2");
        var repeated = await ListWorkItemsAsync(owner, workspace.Id, "?pageSize=2&page=2");
        Assert.AreEqual(4, first.TotalCount);
        Assert.AreEqual(4, second.TotalCount);
        Assert.AreEqual(2, second.Page);
        Assert.AreEqual(2, second.PageSize);
        CollectionAssert.AreEqual(expected, first.Items.Concat(second.Items).Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(second.Items.Select(item => item.Id).ToArray(),
            repeated.Items.Select(item => item.Id).ToArray());

        var beyondEnd = await ListWorkItemsAsync(owner, workspace.Id, "?page=10000&pageSize=100");
        Assert.AreEqual(4, beyondEnd.TotalCount);
        Assert.IsEmpty(beyondEnd.Items);

        var cases = new (string Query, Guid[] Ids)[]
        {
            ("?search=%20aPi%20", [items[0].Id, items[1].Id, items[2].Id]),
            ("?status=inprogress", [items[1].Id]),
            ($"?projectId={items[0].ProjectId:D}", [items[0].Id, items[1].Id, items[2].Id]),
            ($"?assigneeUserId={items[0].AssigneeUserId:D}", [items[0].Id, items[1].Id]),
            ($"?search=api&status=InProgress&projectId={items[0].ProjectId:D}&assigneeUserId={items[0].AssigneeUserId:D}", [items[1].Id]),
            ("?search=%5C", [items[2].Id]),
            ("?search=queue", [items[3].Id]),
            ("?status=Completed", []),
            ("?search=no-matching-title", []),
            ($"?projectId={Guid.NewGuid():D}", []),
            ($"?assigneeUserId={Guid.NewGuid():D}", []),
            ("?search=%20&status=%20", expected),
        };
        foreach (var (query, ids) in cases)
        {
            var result = await ListWorkItemsAsync(owner, workspace.Id, query);
            Assert.AreEqual(ids.Length, result.TotalCount, query);
            CollectionAssert.AreEquivalent(ids, result.Items.Select(item => item.Id).ToArray(), query);
        }
    }

    [TestMethod]
    public async Task Work_item_listing_requires_membership_and_never_leaks_foreign_rows_or_counts()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var owner = CreateAuthorizedClient($"functional|list-owner-{suffix}", "List Owner");
        using var outsider = CreateAuthorizedClient($"functional|list-outsider-{suffix}", "List Outsider");
        using var viewer = CreateAuthorizedClient($"functional|list-viewer-{suffix}", "List Viewer");
        using var contributor = CreateAuthorizedClient($"functional|list-contributor-{suffix}", "List Contributor");
        using var anonymous = CreateClient();
        var (workspace, _) = await SeedWorkItemListAsync(owner);
        var (foreign, foreignItems) = await SeedWorkItemListAsync(outsider);
        await InviteMemberAsync(owner, workspace.Id, $"functional|list-viewer-{suffix}", "List Viewer", WorkspaceRole.Viewer);
        await InviteMemberAsync(owner, workspace.Id, $"functional|list-contributor-{suffix}", "List Contributor", WorkspaceRole.ProjectContributor);

        foreach (var client in new[] { owner, viewer, contributor })
        {
            var visible = await ListWorkItemsAsync(client, workspace.Id, "?search=api");
            Assert.AreEqual(3, visible.TotalCount);
            Assert.IsFalse(visible.Items.Any(item => foreignItems.Any(other => other.Id == item.Id)));
            var byForeignProject = await ListWorkItemsAsync(client, workspace.Id, $"?projectId={foreignItems[0].ProjectId:D}");
            var byForeignAssignee = await ListWorkItemsAsync(client, workspace.Id, $"?assigneeUserId={foreignItems[0].AssigneeUserId:D}");
            Assert.AreEqual(0, byForeignProject.TotalCount);
            Assert.IsEmpty(byForeignProject.Items);
            Assert.AreEqual(0, byForeignAssignee.TotalCount);
            Assert.IsEmpty(byForeignAssignee.Items);
        }

        using var denied = await SendWorkspaceAsync(outsider, HttpMethod.Get, "/api/v1/work-items/", workspace.Id);
        Assert.AreEqual(HttpStatusCode.NotFound, denied.StatusCode);
        using var foreignDenied = await SendWorkspaceAsync(viewer, HttpMethod.Get, "/api/v1/work-items/", foreign.Id);
        Assert.AreEqual(HttpStatusCode.NotFound, foreignDenied.StatusCode);
        using var unauthenticated = await SendWorkspaceAsync(anonymous, HttpMethod.Get, "/api/v1/work-items/", workspace.Id);
        Assert.AreEqual(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        using var missingContext = await owner.GetAsync(new Uri("/api/v1/work-items/", UriKind.Relative));
        Assert.AreEqual(HttpStatusCode.BadRequest, missingContext.StatusCode);
    }

    [TestMethod]
    [DataRow("page=0", "invalid_pagination")]
    [DataRow("page=10001", "invalid_pagination")]
    [DataRow("pageSize=0", "invalid_pagination")]
    [DataRow("pageSize=101", "invalid_pagination")]
    [DataRow("page=2147483647", "invalid_pagination")]
    [DataRow("status=Unknown", "invalid_work_item_status")]
    [DataRow("status=1", "invalid_work_item_status")]
    [DataRow("projectId=00000000-0000-0000-0000-000000000000", "invalid_work_item_filter")]
    [DataRow("assigneeUserId=00000000-0000-0000-0000-000000000000", "invalid_work_item_filter")]
    [DataRow("search=%25", "input_rejected")]
    [DataRow("search=%5F", "input_rejected")]
    [DataRow("search=%3Cscript%3E", "input_rejected")]
    [DataRow("search=unsafe%0Avalue", "input_rejected")]
    public async Task Work_item_listing_rejects_invalid_query_values(string query, string code)
    {
        using var owner = CreateAuthorizedClient($"functional|list-invalid-{Guid.NewGuid():N}", "Query Owner");
        var workspace = await CreateWorkspaceAsync(owner, "Query Validation");
        using var response = await SendWorkspaceAsync(owner, HttpMethod.Get, $"/api/v1/work-items/?{query}", workspace.Id);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await AssertProblemCodeAsync(response, code);
    }

    [TestMethod]
    [DataRow("page=abc")]
    [DataRow("pageSize=2147483648")]
    [DataRow("projectId=not-a-guid")]
    [DataRow("assigneeUserId=not-a-guid")]
    public async Task Work_item_listing_rejects_malformed_typed_query_values(string query)
    {
        using var owner = CreateAuthorizedClient($"functional|list-malformed-{Guid.NewGuid():N}", "Query Owner");
        var workspace = await CreateWorkspaceAsync(owner, "Query Binding");
        using var response = await SendWorkspaceAsync(owner, HttpMethod.Get, $"/api/v1/work-items/?{query}", workspace.Id);
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<PagedResponse<WorkItemResponse>> ListWorkItemsAsync(
        HttpClient client, Guid workspaceId, string query)
    {
        using var response = await SendWorkspaceAsync(client, HttpMethod.Get, $"/api/v1/work-items/{query}", workspaceId);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<WorkItemResponse>>();
        Assert.IsNotNull(result);
        return result;
    }

    private static async Task<(WorkspaceResponse Workspace, WorkItem[] Items)> SeedWorkItemListAsync(HttpClient owner)
    {
        var workspace = await CreateWorkspaceAsync(owner, "Work Item Queries");
        var me = await GetMeAsync(owner);
        var first = await CreateProjectAsync(owner, workspace.Id, "First Project", "first-project");
        var second = await CreateProjectAsync(owner, workspace.Id, "Second Project", "second-project");
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var workspaceId = WorkspaceId.From(workspace.Id);
        WorkItem[] items =
        [
            WorkItem.Create(workspaceId, first.Id, "Deliver API", WorkItemPriority.High, me.UserId, ["backend"], now),
            WorkItem.Create(workspaceId, first.Id, "Document API", WorkItemPriority.Normal, me.UserId, [], now),
            WorkItem.Create(workspaceId, first.Id, @"Repair C:\API", WorkItemPriority.Normal, null, [], now),
            WorkItem.Create(workspaceId, second.Id, "Review queue", WorkItemPriority.Low, null, [], now.AddMinutes(-1)),
        ];
        items[1].TransitionTo(WorkItemStatus.InProgress, now);
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IWorkspaceContextAccessor>().Establish(
            new WorkspaceContext(me.UserId, workspaceId, WorkspaceRole.Owner, WorkspaceStatus.Active));
        var context = scope.ServiceProvider.GetRequiredService<WorkOpsDbContext>();
        context.WorkItems.AddRange(items);
        await context.SaveChangesAsync();
        return (workspace, items);
    }
}
