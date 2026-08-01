using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Messaging;
using WorkOps.Application.Tenancy;
using WorkOps.Contracts.Audit;
using WorkOps.Contracts.Common;
using WorkOps.Contracts.Features;
using WorkOps.Contracts.Files;
using WorkOps.Contracts.Identity;
using WorkOps.Contracts.Notifications;
using WorkOps.Contracts.Projects;
using WorkOps.Contracts.Tenancy;
using WorkOps.Contracts.WorkItems;
using WorkOps.Domain;
using WorkOps.Domain.Messaging;
using WorkOps.Domain.Tenancy;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.FunctionalTests;

[TestClass]
public sealed class TenantIdentityEndpointTests
{
    private static readonly string[] ExpectedFlowLabels = ["backend", "tenant-safe"];
    private static WorkOpsWebApplicationFactory _factory = null!;

    [ClassInitialize]
    public static async Task InitializeAsync(TestContext _)
    {
        _factory = new WorkOpsWebApplicationFactory();
        await _factory.InitializeAsync();
    }

    [ClassCleanup]
    public static async Task CleanupAsync()
    {
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task Live_endpoint_is_available_without_a_token()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Protected_endpoint_rejects_missing_and_wrong_audience_tokens()
    {
        using var client = CreateClient();

        using var missingToken = await client.GetAsync(new Uri("/api/v1/me/", UriKind.Relative));
        Assert.AreEqual(HttpStatusCode.Unauthorized, missingToken.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("functional|wrong-audience", "Wrong Audience", "another-api"));
        using var wrongAudience = await client.GetAsync(new Uri("/api/v1/me/", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.Unauthorized, wrongAudience.StatusCode);
    }

    [TestMethod]
    public async Task Invalid_subject_claims_are_rejected_during_authentication()
    {
        IReadOnlyList<Claim>[] invalidClaims =
        [
            [new Claim("name", "Missing Subject")],
            [new Claim("sub", string.Empty), new Claim("name", "Empty Subject")],
            [new Claim("sub", "first"), new Claim("sub", "second"), new Claim("name", "Duplicate Subject")],
            [new Claim("sub", new string('a', 256)), new Claim("name", "Long Subject")],
            [new Claim("sub", "non-ascii-é"), new Claim("name", "Non ASCII Subject")],
        ];

        foreach (var claims in invalidClaims)
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                CreateToken(claims, WorkOpsWebApplicationFactory.Audience));

            using var response = await client.GetAsync(new Uri("/api/v1/me/", UriKind.Relative));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [TestMethod]
    public async Task Valid_subjects_are_preserved_exactly_and_remain_case_sensitive()
    {
        string[] subjects =
        [
            new string('a', 255),
            " leading-and-trailing ",
            "Case-Sensitive",
            "case-sensitive",
        ];

        foreach (var subject in subjects)
        {
            using var client = CreateAuthorizedClient(subject, "Exact Subject User");
            using var response = await client.GetAsync(new Uri("/api/v1/me/", UriKind.Relative));
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkOpsDbContext>();
        var storedSubjects = await dbContext.Users
            .Where(user => subjects.Contains(user.Subject))
            .Select(user => user.Subject)
            .ToArrayAsync();

        CollectionAssert.AreEquivalent(subjects, storedSubjects);
    }

    [TestMethod]
    public async Task Invitation_rejects_an_invalid_identity_subject()
    {
        using var ownerClient = CreateAuthorizedClient(
            $"functional|subject-owner-{Guid.NewGuid():N}",
            "Subject Owner");
        var workspace = await CreateWorkspaceAsync(ownerClient, "Subject Boundary Team");

        using var response = await ownerClient.PostAsJsonAsync(
            new Uri($"/api/v1/workspaces/{workspace.Id:D}/invitations", UriKind.Relative),
            new InviteWorkspaceMemberRequest(
                new string('a', 256),
                "Invalid Subject",
                WorkspaceRole.Viewer.ToString()));

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await AssertProblemCodeAsync(response, "invalid_identity_subject");
    }

    [TestMethod]
    public async Task Cross_workspace_access_is_hidden_and_capabilities_match_the_membership()
    {
        using var firstClient = CreateAuthorizedClient("functional|owner-a", "Owner A");
        using var secondClient = CreateAuthorizedClient("functional|owner-b", "Owner B");
        var first = await CreateWorkspaceAsync(firstClient, "First Team");
        var second = await CreateWorkspaceAsync(secondClient, "Second Team");

        using var ownWorkspace = await firstClient.GetAsync(
            new Uri($"/api/v1/workspaces/{first.Id:D}", UriKind.Relative));
        Assert.AreEqual(HttpStatusCode.OK, ownWorkspace.StatusCode);

        using var foreignWorkspace = await firstClient.GetAsync(
            new Uri($"/api/v1/workspaces/{second.Id:D}", UriKind.Relative));
        Assert.AreEqual(HttpStatusCode.NotFound, foreignWorkspace.StatusCode);

        using var capabilitiesRequest = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("/api/v1/me/capabilities", UriKind.Relative));
        capabilitiesRequest.Headers.Add("X-Workspace-Id", first.Id.ToString("D"));
        using var capabilitiesResponse = await firstClient.SendAsync(capabilitiesRequest);
        var capabilities = await capabilitiesResponse.Content.ReadFromJsonAsync<CapabilitiesResponse>();

        Assert.AreEqual(HttpStatusCode.OK, capabilitiesResponse.StatusCode);
        Assert.IsNotNull(capabilities);
        Assert.AreEqual(WorkspaceRole.Owner.ToString(), capabilities.Role);
        CollectionAssert.Contains(capabilities.Permissions.ToArray(), Permissions.MembersManage);
        CollectionAssert.Contains(capabilities.Permissions.ToArray(), Permissions.WorkspacesManage);
    }

    [TestMethod]
    public async Task Suspended_workspace_is_forbidden_and_inactive_membership_is_hidden()
    {
        using var suspendedClient = CreateAuthorizedClient("functional|suspended", "Suspended Owner");
        using var inactiveClient = CreateAuthorizedClient("functional|inactive", "Inactive Owner");
        var suspended = await CreateWorkspaceAsync(suspendedClient, "Suspended Team");
        var inactive = await CreateWorkspaceAsync(inactiveClient, "Inactive Team");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WorkOpsDbContext>();
            var workspaceContext = scope.ServiceProvider.GetRequiredService<IWorkspaceContextAccessor>();
            var suspendedWorkspace = await dbContext.Workspaces
                .IgnoreQueryFilters()
                .SingleAsync(workspace => workspace.Id == WorkOps.Domain.WorkspaceId.From(suspended.Id));
            var inactiveMembership = await dbContext.WorkspaceMemberships
                .IgnoreQueryFilters()
                .SingleAsync(membership => membership.WorkspaceId == WorkOps.Domain.WorkspaceId.From(inactive.Id));

            suspendedWorkspace.Suspend(DateTimeOffset.UtcNow);
            inactiveMembership.Deactivate(DateTimeOffset.UtcNow);
            workspaceContext.EstablishBackground(WorkOps.Domain.WorkspaceId.From(inactive.Id));
            await dbContext.SaveChangesAsync();
        }

        using var suspendedResponse = await suspendedClient.GetAsync(
            new Uri($"/api/v1/workspaces/{suspended.Id:D}", UriKind.Relative));
        using var inactiveResponse = await inactiveClient.GetAsync(
            new Uri($"/api/v1/workspaces/{inactive.Id:D}", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.Forbidden, suspendedResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, inactiveResponse.StatusCode);
    }

    [DataRow("<script>alert(1)</script>", "valid-team")]
    [DataRow("Valid Team", "../escape")]
    [TestMethod]
    public async Task Workspace_creation_rejects_malicious_input(string name, string slug)
    {
        using var client = CreateAuthorizedClient($"functional|input-{Guid.NewGuid():N}", "Input User");

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/workspaces/", UriKind.Relative),
            new CreateWorkspaceRequest(name, slug));

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [TestMethod]
    public async Task Golden_work_item_flow_rejects_invalid_stale_and_cross_workspace_changes()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var contributorSubject = $"functional|contributor-{suffix}";
        using var ownerClient = CreateAuthorizedClient($"functional|flow-owner-{suffix}", "Flow Owner");
        using var contributorClient = CreateAuthorizedClient(contributorSubject, "Flow Contributor");
        using var outsiderClient = CreateAuthorizedClient($"functional|outsider-{suffix}", "Outsider");
        var workspace = await CreateWorkspaceAsync(ownerClient, "Flow Workspace");
        var outsiderWorkspace = await CreateWorkspaceAsync(outsiderClient, "Outsider Workspace");
        var outsider = await GetMeAsync(outsiderClient);
        var contributor = await InviteMemberAsync(
            ownerClient,
            workspace.Id,
            contributorSubject,
            "Flow Contributor",
            WorkspaceRole.ProjectContributor);
        var project = await CreateProjectAsync(
            ownerClient,
            workspace.Id,
            "Delivery Platform",
            $"delivery-{suffix}");
        var created = await CreateWorkItemAsync(
            contributorClient,
            workspace.Id,
            project.Id,
            contributor.UserId,
            "Deliver tenant workflow");

        Assert.AreEqual("Backlog", created.Status);
        Assert.AreEqual("High", created.Priority);
        Assert.AreEqual(contributor.UserId, created.AssigneeUserId);
        CollectionAssert.AreEqual(ExpectedFlowLabels, created.Labels.ToArray());

        using var invalidAssignment = await SendWorkspaceJsonAsync(
            contributorClient,
            HttpMethod.Patch,
            $"/api/v1/work-items/{created.Id:D}",
            workspace.Id,
            new UpdateWorkItemRequest(
                created.Title,
                created.Priority,
                outsider.UserId,
                created.Labels,
                created.Version));
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, invalidAssignment.StatusCode);
        await AssertProblemCodeAsync(invalidAssignment, "invalid_assignee");

        using var update = await SendWorkspaceJsonAsync(
            contributorClient,
            HttpMethod.Patch,
            $"/api/v1/work-items/{created.Id:D}",
            workspace.Id,
            new UpdateWorkItemRequest(
                "Deliver secure tenant workflow",
                "Critical",
                contributor.UserId,
                ["api", "security"],
                created.Version));
        var updated = await update.Content.ReadFromJsonAsync<WorkItemResponse>();
        Assert.AreEqual(HttpStatusCode.OK, update.StatusCode);
        Assert.IsNotNull(updated);
        Assert.AreEqual("Critical", updated.Priority);
        Assert.AreNotEqual(created.Version, updated.Version);

        using var invalidTransition = await SendWorkspaceJsonAsync(
            contributorClient,
            HttpMethod.Post,
            $"/api/v1/work-items/{created.Id:D}/transitions",
            workspace.Id,
            new TransitionWorkItemRequest("Completed", updated.Version));
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, invalidTransition.StatusCode);

        using var validTransition = await SendWorkspaceJsonAsync(
            contributorClient,
            HttpMethod.Post,
            $"/api/v1/work-items/{created.Id:D}/transitions",
            workspace.Id,
            new TransitionWorkItemRequest("InProgress", updated.Version));
        var transitioned = await validTransition.Content.ReadFromJsonAsync<WorkItemResponse>();
        Assert.AreEqual(HttpStatusCode.OK, validTransition.StatusCode);
        Assert.IsNotNull(transitioned);
        Assert.AreEqual("InProgress", transitioned.Status);
        Assert.AreNotEqual(updated.Version, transitioned.Version);

        Guid outboxMessageId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WorkOpsDbContext>();
            var transitionAudits = await dbContext.AuditEvents
                .IgnoreQueryFilters()
                .Where(auditEvent =>
                    auditEvent.EntityId == created.Id &&
                    auditEvent.Action == AuditActions.WorkItemTransitioned)
                .ToArrayAsync();
            var outboxMessages = await dbContext.OutboxMessages
                .IgnoreQueryFilters()
                .Where(message =>
                    message.WorkspaceId == WorkspaceId.From(workspace.Id) &&
                    message.Type == WorkItemStatusChangedMessage.MessageType)
                .ToArrayAsync();

            Assert.HasCount(1, transitionAudits);
            Assert.HasCount(1, outboxMessages);
            Assert.AreEqual(OutboxMessageStatus.Pending, outboxMessages[0].Status);
            Assert.Contains(created.Id.ToString("D"), outboxMessages[0].PayloadJson, StringComparison.Ordinal);
            Assert.DoesNotContain("Deliver secure", outboxMessages[0].PayloadJson, StringComparison.Ordinal);
            outboxMessageId = outboxMessages[0].Id;
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
            Assert.AreEqual(
                OutboxProcessResult.Published,
                await processor.ProcessNextAsync(CancellationToken.None));
        }

        var published = _factory.Publisher.Messages.Single(message => message.Id == outboxMessageId);
        bool firstDelivery;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<NotificationMessageHandler>();
            firstDelivery = await handler.HandleAsync(
                published.Type,
                published.PayloadJson,
                CancellationToken.None);
        }

        bool duplicateDelivery;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<NotificationMessageHandler>();
            duplicateDelivery = await handler.HandleAsync(
                published.Type,
                published.PayloadJson,
                CancellationToken.None);
        }

        Assert.IsTrue(firstDelivery);
        Assert.IsFalse(duplicateDelivery);

        using var notificationsResponse = await SendWorkspaceAsync(
            contributorClient,
            HttpMethod.Get,
            "/api/v1/notifications?page=1&pageSize=20",
            workspace.Id);
        var notifications = await notificationsResponse.Content
            .ReadFromJsonAsync<PagedResponse<NotificationResponse>>();
        Assert.AreEqual(HttpStatusCode.OK, notificationsResponse.StatusCode);
        Assert.IsNotNull(notifications);
        Assert.AreEqual(1, notifications.TotalCount);
        Assert.HasCount(1, notifications.Items);
        Assert.AreEqual(created.Id, notifications.Items[0].EntityId);
        Assert.AreEqual("development", notifications.Items[0].Channel);

        using var auditResponse = await SendWorkspaceAsync(
            ownerClient,
            HttpMethod.Get,
            "/api/v1/audit-events?page=1&pageSize=20&action=work_item.transitioned&entityType=work_item",
            workspace.Id);
        var auditPage = await auditResponse.Content.ReadFromJsonAsync<PagedResponse<AuditEventResponse>>();
        Assert.AreEqual(HttpStatusCode.OK, auditResponse.StatusCode);
        Assert.IsNotNull(auditPage);
        Assert.AreEqual(1, auditPage.TotalCount);
        Assert.AreEqual("InProgress", auditPage.Items[0].Metadata["currentStatus"]);

        using var replayProcessed = await SendWorkspaceAsync(
            ownerClient,
            HttpMethod.Post,
            $"/api/v1/operations/outbox/{outboxMessageId:D}/replay",
            workspace.Id);
        Assert.AreEqual(HttpStatusCode.Conflict, replayProcessed.StatusCode);
        await AssertProblemCodeAsync(replayProcessed, "outbox_replay_not_allowed");

        using var staleTransition = await SendWorkspaceJsonAsync(
            contributorClient,
            HttpMethod.Post,
            $"/api/v1/work-items/{created.Id:D}/transitions",
            workspace.Id,
            new TransitionWorkItemRequest("Blocked", updated.Version));
        Assert.AreEqual(HttpStatusCode.Conflict, staleTransition.StatusCode);
        await AssertProblemCodeAsync(staleTransition, "concurrency_conflict");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WorkOpsDbContext>();
            Assert.AreEqual(
                1,
                await dbContext.OutboxMessages
                    .IgnoreQueryFilters()
                    .CountAsync(message =>
                        message.WorkspaceId == WorkspaceId.From(workspace.Id) &&
                        message.Type == WorkItemStatusChangedMessage.MessageType));
        }

        using var outsiderRead = await SendWorkspaceAsync(
            outsiderClient,
            HttpMethod.Get,
            $"/api/v1/work-items/{created.Id:D}",
            outsiderWorkspace.Id);
        Assert.AreEqual(HttpStatusCode.NotFound, outsiderRead.StatusCode);

        using var projectList = await SendWorkspaceAsync(
            ownerClient,
            HttpMethod.Get,
            "/api/v1/projects/?page=1&pageSize=1&search=Delivery&status=Active",
            workspace.Id);
        var page = await projectList.Content.ReadFromJsonAsync<PagedResponse<ProjectResponse>>();
        Assert.AreEqual(HttpStatusCode.OK, projectList.StatusCode);
        Assert.IsNotNull(page);
        Assert.AreEqual(1, page.TotalCount);
        Assert.HasCount(1, page.Items);
        Assert.AreEqual(1, page.Items[0].WorkItemCount);
    }

    [TestMethod]
    public async Task Viewer_cannot_write_and_archived_project_rejects_new_work_items()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var viewerSubject = $"functional|viewer-{suffix}";
        using var ownerClient = CreateAuthorizedClient($"functional|archive-owner-{suffix}", "Archive Owner");
        using var viewerClient = CreateAuthorizedClient(viewerSubject, "Project Viewer");
        var workspace = await CreateWorkspaceAsync(ownerClient, "Archive Workspace");
        await InviteMemberAsync(
            ownerClient,
            workspace.Id,
            viewerSubject,
            "Project Viewer",
            WorkspaceRole.Viewer);
        var project = await CreateProjectAsync(
            ownerClient,
            workspace.Id,
            "Archive Project",
            $"archive-{suffix}");

        using var viewerWrite = await SendWorkspaceJsonAsync(
            viewerClient,
            HttpMethod.Post,
            "/api/v1/projects/",
            workspace.Id,
            new CreateProjectRequest("Forbidden Project", $"forbidden-{suffix}"));
        Assert.AreEqual(HttpStatusCode.Forbidden, viewerWrite.StatusCode);

        using var viewerAudit = await SendWorkspaceAsync(
            viewerClient,
            HttpMethod.Get,
            "/api/v1/audit-events",
            workspace.Id);
        Assert.AreEqual(HttpStatusCode.Forbidden, viewerAudit.StatusCode);

        using var archive = await SendWorkspaceAsync(
            ownerClient,
            HttpMethod.Post,
            $"/api/v1/projects/{project.Id:D}/archive",
            workspace.Id);
        Assert.AreEqual(HttpStatusCode.NoContent, archive.StatusCode);

        using var createAfterArchive = await SendWorkspaceJsonAsync(
            ownerClient,
            HttpMethod.Post,
            $"/api/v1/projects/{project.Id:D}/work-items",
            workspace.Id,
            new CreateWorkItemRequest("Should fail", "Normal", null, []));
        Assert.AreEqual(HttpStatusCode.Conflict, createAfterArchive.StatusCode);
        await AssertProblemCodeAsync(createAfterArchive, "project_archived");
    }

    [TestMethod]
    public async Task Starter_plan_enforces_project_limit_and_plan_changes_invalidate_features()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var viewerSubject = $"functional|limit-viewer-{suffix}";
        using var ownerClient = CreateAuthorizedClient($"functional|limit-owner-{suffix}", "Limit Owner");
        using var viewerClient = CreateAuthorizedClient(viewerSubject, "Limit Viewer");
        var workspace = await CreateWorkspaceAsync(ownerClient, "Limited Workspace");
        await InviteMemberAsync(
            ownerClient,
            workspace.Id,
            viewerSubject,
            "Limit Viewer",
            WorkspaceRole.Viewer);
        var firstProject = await CreateProjectAsync(
            ownerClient,
            workspace.Id,
            "First Project",
            $"first-{suffix}");
        await CreateProjectAsync(
            ownerClient,
            workspace.Id,
            "Second Project",
            $"second-{suffix}");

        using var limitReached = await SendWorkspaceJsonAsync(
            ownerClient,
            HttpMethod.Post,
            "/api/v1/projects/",
            workspace.Id,
            new CreateProjectRequest("Third Project", $"third-{suffix}"));
        Assert.AreEqual(HttpStatusCode.Conflict, limitReached.StatusCode);
        await AssertProblemCodeAsync(limitReached, "feature_limit_exceeded");

        var starterFeatures = await GetFeaturesAsync(ownerClient, workspace.Id);
        Assert.AreEqual("Starter", starterFeatures.Plan);
        Assert.AreEqual(2, starterFeatures.MaximumActiveProjects);
        Assert.AreEqual(2, starterFeatures.ActiveProjectCount);

        using var viewerPlanChange = await viewerClient.PutAsJsonAsync(
            new Uri($"/api/v1/workspaces/{workspace.Id:D}/plan", UriKind.Relative),
            new UpdateWorkspacePlanRequest("Team"));
        Assert.AreEqual(HttpStatusCode.Forbidden, viewerPlanChange.StatusCode);

        using var planChange = await ownerClient.PutAsJsonAsync(
            new Uri($"/api/v1/workspaces/{workspace.Id:D}/plan", UriKind.Relative),
            new UpdateWorkspacePlanRequest("Team"));
        var teamFeatures = await planChange.Content.ReadFromJsonAsync<FeatureEntitlementsResponse>();
        Assert.AreEqual(HttpStatusCode.OK, planChange.StatusCode);
        Assert.IsNotNull(teamFeatures);
        Assert.AreEqual("Team", teamFeatures.Plan);
        Assert.AreEqual(20, teamFeatures.MaximumActiveProjects);

        await CreateProjectAsync(
            ownerClient,
            workspace.Id,
            "Third Project",
            $"third-{suffix}");
        using var archive = await SendWorkspaceAsync(
            ownerClient,
            HttpMethod.Post,
            $"/api/v1/projects/{firstProject.Id:D}/archive",
            workspace.Id);
        Assert.AreEqual(HttpStatusCode.NoContent, archive.StatusCode);

        var afterArchive = await GetFeaturesAsync(ownerClient, workspace.Id);
        Assert.AreEqual(2, afterArchive.ActiveProjectCount);
    }

    [TestMethod]
    public async Task Attachment_upload_validates_content_and_downloads_only_within_tenant()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var ownerClient = CreateAuthorizedClient($"functional|file-owner-{suffix}", "File Owner");
        using var outsiderClient = CreateAuthorizedClient(
            $"functional|file-outsider-{suffix}",
            "File Outsider");
        var workspace = await CreateWorkspaceAsync(ownerClient, "File Workspace");
        var outsiderWorkspace = await CreateWorkspaceAsync(outsiderClient, "Other File Workspace");
        var owner = await GetMeAsync(ownerClient);
        var project = await CreateProjectAsync(
            ownerClient,
            workspace.Id,
            "File Project",
            $"files-{suffix}");
        var workItem = await CreateWorkItemAsync(
            ownerClient,
            workspace.Id,
            project.Id,
            owner.UserId,
            "Store safe evidence");
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02 };

        using var upload = await SendWorkspaceFileAsync(
            ownerClient,
            $"/api/v1/work-items/{workItem.Id:D}/attachments",
            workspace.Id,
            "evidence.png",
            "image/png",
            png);
        var attachment = await upload.Content.ReadFromJsonAsync<AttachmentResponse>();
        Assert.AreEqual(HttpStatusCode.Created, upload.StatusCode);
        Assert.IsNotNull(attachment);
        Assert.AreEqual("evidence.png", attachment.FileName);
        Assert.AreEqual(png.Length, attachment.Size);

        using var download = await SendWorkspaceAsync(
            ownerClient,
            HttpMethod.Get,
            $"/api/v1/attachments/{attachment.Id:D}",
            workspace.Id);
        Assert.AreEqual(HttpStatusCode.OK, download.StatusCode);
        Assert.AreEqual("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.AreEqual("image/png", download.Content.Headers.ContentType?.MediaType);
        CollectionAssert.AreEqual(png, await download.Content.ReadAsByteArrayAsync());

        using var outsiderDownload = await SendWorkspaceAsync(
            outsiderClient,
            HttpMethod.Get,
            $"/api/v1/attachments/{attachment.Id:D}",
            outsiderWorkspace.Id);
        Assert.AreEqual(HttpStatusCode.NotFound, outsiderDownload.StatusCode);

        WorkOps.Domain.Files.Attachment storedAttachment;
        IFileStorage storage;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WorkOpsDbContext>();
            storedAttachment = await dbContext.Attachments
                .IgnoreQueryFilters()
                .SingleAsync(candidate => candidate.Id == attachment.Id);
            storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        }

        await storage.DeleteAsync(
            storedAttachment.WorkspaceId,
            storedAttachment.StorageName,
            CancellationToken.None);
        using var missingContent = await SendWorkspaceAsync(
            ownerClient,
            HttpMethod.Get,
            $"/api/v1/attachments/{attachment.Id:D}",
            workspace.Id);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, missingContent.StatusCode);
        await AssertProblemCodeAsync(missingContent, "attachment_content_unavailable");

        var corruptPng = png.ToArray();
        corruptPng[^1] ^= 0xFF;
        await storage.SaveAsync(
            storedAttachment.WorkspaceId,
            storedAttachment.StorageName,
            corruptPng,
            CancellationToken.None);
        using var corruptContent = await SendWorkspaceAsync(
            ownerClient,
            HttpMethod.Get,
            $"/api/v1/attachments/{attachment.Id:D}",
            workspace.Id);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, corruptContent.StatusCode);
        await AssertProblemCodeAsync(corruptContent, "attachment_content_unavailable");

        using var mismatch = await SendWorkspaceFileAsync(
            ownerClient,
            $"/api/v1/work-items/{workItem.Id:D}/attachments",
            workspace.Id,
            "disguised.png",
            "image/png",
            "%PDF-1.7"u8.ToArray());
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, mismatch.StatusCode);
        await AssertProblemCodeAsync(mismatch, "invalid_attachment_type");

        using var pathTraversal = await SendWorkspaceFileAsync(
            ownerClient,
            $"/api/v1/work-items/{workItem.Id:D}/attachments",
            workspace.Id,
            "../escape.txt",
            "text/plain",
            "safe"u8.ToArray());
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, pathTraversal.StatusCode);
        await AssertProblemCodeAsync(pathTraversal, "input_rejected");

        using var oversized = await SendWorkspaceFileAsync(
            ownerClient,
            $"/api/v1/work-items/{workItem.Id:D}/attachments",
            workspace.Id,
            "large.txt",
            "text/plain",
            new byte[524_289]);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, oversized.StatusCode);
        await AssertProblemCodeAsync(oversized, "invalid_attachment_size");
    }

    [TestMethod]
    public async Task Responses_include_safe_diagnostics_and_security_headers()
    {
        const string privateMarker = "private-marker-741963";
        _factory.Logs.Clear();
        using var client = CreateAuthorizedClient(
            $"functional|diagnostics-{Guid.NewGuid():N}",
            "Diagnostics User");

        using var health = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        Assert.AreEqual(HttpStatusCode.OK, health.StatusCode);
        Assert.AreEqual("nosniff", health.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.AreEqual("DENY", health.Headers.GetValues("X-Frame-Options").Single());
        Assert.AreEqual("no-referrer", health.Headers.GetValues("Referrer-Policy").Single());
        Assert.IsFalse(health.Headers.Contains("Server"));
        var correlationId = health.Headers.GetValues("X-Correlation-Id").Single();
        Assert.AreEqual(32, correlationId.Length);

        using var rejected = await client.PostAsJsonAsync(
            new Uri("/api/v1/workspaces/", UriKind.Relative),
            new CreateWorkspaceRequest($"<{privateMarker}>", "safe-diagnostics"));
        var body = await rejected.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.AreEqual("input_rejected", problem.RootElement.GetProperty("code").GetString());
        Assert.AreEqual(
            rejected.Headers.GetValues("X-Correlation-Id").Single(),
            problem.RootElement.GetProperty("correlationId").GetString());
        Assert.AreEqual(32, problem.RootElement.GetProperty("traceId").GetString()?.Length);
        Assert.DoesNotContain(privateMarker, body, StringComparison.Ordinal);
        Assert.IsFalse(_factory.Logs.Messages.Any(message =>
            message.Contains(privateMarker, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Openapi_and_untrusted_cors_are_not_exposed_outside_development()
    {
        using var client = CreateClient();

        using var openApi = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        Assert.AreEqual(HttpStatusCode.NotFound, openApi.StatusCode);

        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            new Uri("/api/v1/me/", UriKind.Relative));
        request.Headers.Add("Origin", "https://untrusted.example.invalid");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var preflight = await client.SendAsync(request);

        Assert.IsFalse(preflight.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [TestMethod]
    public async Task Rate_limit_returns_safe_problem_details()
    {
        var subject = $"functional|rate-limit-{Guid.NewGuid():N}";
        var options = _factory.Services
            .GetRequiredService<IOptions<RateLimiterOptions>>()
            .Value;
        var limiter = options.GlobalLimiter;
        Assert.IsNotNull(limiter);
        var permitLimit = _factory.Services
            .GetRequiredService<IConfiguration>()
            .GetValue<int>("RateLimiting:PermitLimit");
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/me/";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", subject)],
            authenticationType: "test"));

        for (var attempt = 0; attempt < permitLimit; attempt++)
        {
            using var lease = await limiter.AcquireAsync(context, 1, CancellationToken.None);
            Assert.IsTrue(lease.IsAcquired);
        }

        using var client = CreateAuthorizedClient(subject, "Rate Limited User");
        using var response = await client.GetAsync(new Uri("/api/v1/me/", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.AreEqual("60", response.Headers.RetryAfter?.Delta?.TotalSeconds.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ??
            response.Headers.GetValues("Retry-After").Single());
        await AssertProblemCodeAsync(response, "rate_limit_exceeded");
    }

    [TestMethod]
    public async Task Project_creation_replays_only_the_same_scoped_idempotent_request()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var idempotencyKey = $"project-{suffix}";
        using var ownerClient = CreateAuthorizedClient(
            $"functional|idempotency-owner-{suffix}",
            "Idempotency Owner");
        using var otherClient = CreateAuthorizedClient(
            $"functional|idempotency-other-{suffix}",
            "Other Owner");
        var workspace = await CreateWorkspaceAsync(ownerClient, "Idempotent Workspace");
        var otherWorkspace = await CreateWorkspaceAsync(otherClient, "Other Idempotent Workspace");
        var body = new CreateProjectRequest("Retry Safe Project", $"retry-{suffix}");

        using var first = await SendIdempotentProjectAsync(
            ownerClient,
            workspace.Id,
            idempotencyKey,
            body);
        using var second = await SendIdempotentProjectAsync(
            ownerClient,
            workspace.Id,
            idempotencyKey,
            body);
        var firstProject = await first.Content.ReadFromJsonAsync<ProjectResponse>();
        var secondProject = await second.Content.ReadFromJsonAsync<ProjectResponse>();

        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, second.StatusCode);
        Assert.IsNotNull(firstProject);
        Assert.IsNotNull(secondProject);
        Assert.AreEqual(firstProject, secondProject);
        Assert.IsFalse(first.Headers.Contains("Idempotency-Replayed"));
        Assert.AreEqual("true", second.Headers.GetValues("Idempotency-Replayed").Single());

        using var changedBody = await SendIdempotentProjectAsync(
            ownerClient,
            workspace.Id,
            idempotencyKey,
            new CreateProjectRequest("Changed Request", $"changed-{suffix}"));
        Assert.AreEqual(HttpStatusCode.Conflict, changedBody.StatusCode);
        await AssertProblemCodeAsync(changedBody, "idempotency_key_conflict");

        using var otherScope = await SendIdempotentProjectAsync(
            otherClient,
            otherWorkspace.Id,
            idempotencyKey,
            new CreateProjectRequest("Independent Project", $"independent-{suffix}"));
        Assert.AreEqual(HttpStatusCode.Created, otherScope.StatusCode);

        var features = await GetFeaturesAsync(ownerClient, workspace.Id);
        Assert.AreEqual(1, features.ActiveProjectCount);
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkOpsDbContext>();
        Assert.AreEqual(
            2,
            await dbContext.IdempotencyRecords.IgnoreQueryFilters().CountAsync(record =>
                record.Key == idempotencyKey));
    }

    [TestMethod]
    public async Task Production_hides_openapi_and_emits_hsts()
    {
        await using var productionFactory = new WorkOpsWebApplicationFactory("Production");
        await productionFactory.InitializeAsync();
        using var client = productionFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://workops.test"),
        });

        using var health = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        using var openApi = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, health.StatusCode);
        Assert.IsTrue(health.Headers.Contains("Strict-Transport-Security"));
        Assert.AreEqual(HttpStatusCode.NotFound, openApi.StatusCode);
    }

    private static HttpClient CreateClient() => _factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static HttpClient CreateAuthorizedClient(string subject, string displayName)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(subject, displayName, WorkOpsWebApplicationFactory.Audience));
        return client;
    }

    private static string CreateToken(string subject, string displayName, string audience)
        => CreateToken(
            [new Claim("sub", subject), new Claim("name", displayName)],
            audience);

    private static string CreateToken(IReadOnlyCollection<Claim> claims, string audience)
    {
        var token = new JwtSecurityToken(
            issuer: WorkOpsWebApplicationFactory.Issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                WorkOpsWebApplicationFactory.SigningKey,
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<WorkspaceResponse> CreateWorkspaceAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/workspaces/", UriKind.Relative),
            new CreateWorkspaceRequest(name, $"team-{Guid.NewGuid():N}"));
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(workspace);
        return workspace;
    }

    private static async Task<WorkspaceMemberResponse> InviteMemberAsync(
        HttpClient client,
        Guid workspaceId,
        string subject,
        string displayName,
        WorkspaceRole role)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/workspaces/{workspaceId:D}/invitations", UriKind.Relative),
            new InviteWorkspaceMemberRequest(subject, displayName, role.ToString()));
        var member = await response.Content.ReadFromJsonAsync<WorkspaceMemberResponse>();

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(member);
        return member;
    }

    private static async Task<MeResponse> GetMeAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri("/api/v1/me/", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.IsNotNull(me);
        return me;
    }

    private static async Task<ProjectResponse> CreateProjectAsync(
        HttpClient client,
        Guid workspaceId,
        string name,
        string key)
    {
        using var response = await SendWorkspaceJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/projects/",
            workspaceId,
            new CreateProjectRequest(name, key));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.IsNotNull(project);
        return project;
    }

    private static async Task<WorkItemResponse> CreateWorkItemAsync(
        HttpClient client,
        Guid workspaceId,
        Guid projectId,
        Guid assigneeUserId,
        string title)
    {
        using var response = await SendWorkspaceJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/projects/{projectId:D}/work-items",
            workspaceId,
            new CreateWorkItemRequest(
                title,
                "High",
                assigneeUserId,
                ["tenant-safe", "backend"]));
        var workItem = await response.Content.ReadFromJsonAsync<WorkItemResponse>();

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(workItem);
        return workItem;
    }

    private static async Task<FeatureEntitlementsResponse> GetFeaturesAsync(
        HttpClient client,
        Guid workspaceId)
    {
        using var response = await SendWorkspaceAsync(
            client,
            HttpMethod.Get,
            "/api/v1/features",
            workspaceId);
        var features = await response.Content.ReadFromJsonAsync<FeatureEntitlementsResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(features);
        return features;
    }

    private static async Task<HttpResponseMessage> SendIdempotentProjectAsync(
        HttpClient client,
        Guid workspaceId,
        string idempotencyKey,
        CreateProjectRequest body)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri("/api/v1/projects/", UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Workspace-Id", workspaceId.ToString("D"));
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendWorkspaceFileAsync(
        HttpClient client,
        string path,
        Guid workspaceId,
        string fileName,
        string contentType,
        byte[] content)
    {
        using var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var multipart = new MultipartFormDataContent();
        multipart.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = multipart,
        };
        request.Headers.Add("X-Workspace-Id", workspaceId.ToString("D"));
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendWorkspaceAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        Guid workspaceId)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Workspace-Id", workspaceId.ToString("D"));
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendWorkspaceJsonAsync<T>(
        HttpClient client,
        HttpMethod method,
        string path,
        Guid workspaceId,
        T body)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Workspace-Id", workspaceId.ToString("D"));
        return client.SendAsync(request);
    }

    private static async Task AssertProblemCodeAsync(HttpResponseMessage response, string expectedCode)
    {
        using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.AreEqual(expectedCode, problem.RootElement.GetProperty("code").GetString());
    }
}
