using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkOps.Application.Audit;
using WorkOps.Application.Tenancy;
using WorkOps.Contracts.Identity;
using WorkOps.Contracts.Tenancy;
using WorkOps.Contracts.WorkItems;
using WorkOps.Domain;
using WorkOps.Domain.Tenancy;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.FunctionalTests;

public sealed partial class TenantIdentityEndpointTests
{
    [TestMethod]
    public async Task Membership_role_and_deactivation_changes_are_versioned_and_audited()
    {
        using var owner = CreateAuthorizedClient($"functional|member-owner-{Guid.NewGuid():N}", "Member Owner");
        var workspace = await CreateWorkspaceAsync(owner, "Lifecycle Workspace");
        var member = await InviteMemberAsync(owner, workspace.Id, $"functional|member-{Guid.NewGuid():N}",
            "Lifecycle Member", WorkspaceRole.Viewer);
        Assert.IsTrue(MembershipVersion.TryDecode(member.Version, out var initialVersion));
        Assert.AreNotEqual(0u, initialVersion);

        var changed = await ChangeMemberRoleAsync(owner, workspace.Id, member, "ProjectContributor");
        Assert.AreNotEqual(member.Version, changed.Version);
        Assert.AreEqual("ProjectContributor", changed.Role);
        var noOp = await ChangeMemberRoleAsync(owner, workspace.Id, changed, changed.Role);
        Assert.AreEqual(changed.Version, noOp.Version);
        using var staleRole = await SendMemberChangeAsync(owner, workspace.Id, member.UserId, "Viewer", member.Version);
        await AssertMemberProblemAsync(staleRole, HttpStatusCode.Conflict, "concurrency_conflict");
        using var staleDeactivation = await SendMemberChangeAsync(owner, workspace.Id, member.UserId, null, member.Version);
        await AssertMemberProblemAsync(staleDeactivation, HttpStatusCode.Conflict, "concurrency_conflict");

        var inactive = await DeactivateMemberAsync(owner, workspace.Id, changed);
        Assert.IsFalse(inactive.IsActive);
        Assert.AreNotEqual(changed.Version, inactive.Version);
        var repeated = await DeactivateMemberAsync(owner, workspace.Id, inactive);
        Assert.AreEqual(inactive.Version, repeated.Version);
        using var inactiveRole = await SendMemberChangeAsync(owner, workspace.Id, member.UserId, "Owner", inactive.Version);
        await AssertMemberProblemAsync(inactiveRole, HttpStatusCode.Conflict, "inactive_workspace_membership");
        var listed = await ReadMemberAsync(owner, workspace.Id, member.UserId);
        Assert.AreEqual(inactive, listed);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IWorkspaceContextAccessor>().EstablishBackground(WorkspaceId.From(workspace.Id));
        var events = await scope.ServiceProvider.GetRequiredService<WorkOpsDbContext>().AuditEvents
            .Where(row => row.EntityId == member.UserId).OrderBy(row => row.OccurredAt).ToArrayAsync();
        CollectionAssert.AreEqual(new[] { AuditActions.MemberInvited, AuditActions.MemberRoleChanged, AuditActions.MemberDeactivated },
            events.Select(row => row.Action).ToArray());
        using var roleMetadata = JsonDocument.Parse(events[1].MetadataJson);
        Assert.AreEqual(2, roleMetadata.RootElement.EnumerateObject().Count());
        Assert.AreEqual("ProjectContributor", roleMetadata.RootElement.GetProperty("currentRole").GetString());
        Assert.AreEqual("Viewer", roleMetadata.RootElement.GetProperty("previousRole").GetString());
        using var deactivationMetadata = JsonDocument.Parse(events[2].MetadataJson);
        Assert.AreEqual(1, deactivationMetadata.RootElement.EnumerateObject().Count());
        Assert.AreEqual("ProjectContributor", deactivationMetadata.RootElement.GetProperty("role").GetString());
        Assert.IsTrue(events.All(row => row.ActorUserId == events[0].ActorUserId && row.EntityType == "workspace_member"));
    }

    [TestMethod]
    public async Task Membership_changes_take_effect_with_the_same_token_and_preserve_other_workspaces()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var owner = CreateAuthorizedClient($"functional|revoke-owner-{suffix}", "Owner");
        using var memberClient = CreateAuthorizedClient($"functional|revoke-member-{suffix}", "Member");
        var workspace = await CreateWorkspaceAsync(owner, "Revoke Workspace");
        var other = await CreateWorkspaceAsync(memberClient, "Unaffected Workspace");
        var member = await InviteMemberAsync(owner, workspace.Id, $"functional|revoke-member-{suffix}", "Member",
            WorkspaceRole.ProjectContributor);
        var project = await CreateProjectAsync(memberClient, workspace.Id, "Member Project", "member-project");
        var item = await CreateWorkItemAsync(memberClient, workspace.Id, project.Id, member.UserId, "Retained Assignment");
        var demoted = await ChangeMemberRoleAsync(owner, workspace.Id, member, "Viewer");
        using var forbidden = await SendWorkspaceJsonAsync(memberClient, HttpMethod.Post, "/api/v1/projects/", workspace.Id,
            new WorkOps.Contracts.Projects.CreateProjectRequest("Denied Project", "denied-project"));
        Assert.AreEqual(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var capabilitiesResponse = await SendWorkspaceAsync(memberClient, HttpMethod.Get, "/api/v1/me/capabilities", workspace.Id);
        var capabilities = await capabilitiesResponse.Content.ReadFromJsonAsync<CapabilitiesResponse>();
        Assert.IsNotNull(capabilities);
        Assert.AreEqual("Viewer", capabilities.Role);
        CollectionAssert.DoesNotContain(capabilities.Permissions.ToArray(), Permissions.ProjectsWrite);

        await DeactivateMemberAsync(owner, workspace.Id, demoted);
        foreach (var path in new[] { "/api/v1/work-items/", $"/api/v1/work-items/{item.Id:D}", "/api/v1/me/capabilities", "/api/v1/notifications/" })
        {
            using var denied = await SendWorkspaceAsync(memberClient, HttpMethod.Get, path, workspace.Id);
            Assert.AreEqual(HttpStatusCode.NotFound, denied.StatusCode, path);
        }

        using var deniedMembership = await memberClient.GetAsync(new Uri($"/api/v1/workspaces/{workspace.Id:D}/members", UriKind.Relative));
        Assert.AreEqual(HttpStatusCode.NotFound, deniedMembership.StatusCode);
        var me = await GetMeAsync(memberClient);
        Assert.IsFalse(me.Memberships.Any(row => row.WorkspaceId == workspace.Id));
        Assert.IsTrue(me.Memberships.Any(row => row.WorkspaceId == other.Id && row.Role == "Owner"));
        using var unchanged = await SendWorkspaceAsync(owner, HttpMethod.Get, $"/api/v1/work-items/{item.Id:D}", workspace.Id);
        var retained = await unchanged.Content.ReadFromJsonAsync<WorkItemResponse>();
        Assert.IsNotNull(retained);
        Assert.AreEqual(member.UserId, retained.AssigneeUserId);
        using var invalidAssignment = await SendWorkspaceJsonAsync(owner, HttpMethod.Post,
            $"/api/v1/projects/{project.Id:D}/work-items", workspace.Id,
            new CreateWorkItemRequest("Invalid Assignment", "Normal", member.UserId, []));
        await AssertMemberProblemAsync(invalidAssignment, HttpStatusCode.UnprocessableEntity, "invalid_assignee");
    }

    [TestMethod]
    public async Task Membership_administrators_cannot_grant_privilege_or_modify_privileged_members()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var owner = CreateAuthorizedClient($"functional|matrix-owner-{suffix}", "Owner");
        using var admin = CreateAuthorizedClient($"functional|matrix-admin-{suffix}", "Administrator");
        var workspace = await CreateWorkspaceAsync(owner, "Authority Workspace");
        var adminMember = await InviteMemberAsync(owner, workspace.Id, $"functional|matrix-admin-{suffix}", "Administrator", WorkspaceRole.Viewer);
        adminMember = await ChangeMemberRoleAsync(owner, workspace.Id, adminMember, "Administrator");
        var ordinary = await InviteMemberAsync(admin, workspace.Id, $"functional|matrix-target-{suffix}", "Target", WorkspaceRole.Viewer);
        ordinary = await ChangeMemberRoleAsync(admin, workspace.Id, ordinary, "ProjectContributor");
        ordinary = await ChangeMemberRoleAsync(admin, workspace.Id, ordinary, "Viewer");

        foreach (var role in new[] { "Owner", "Administrator" })
        {
            using var escalation = await SendMemberChangeAsync(admin, workspace.Id, ordinary.UserId, role, ordinary.Version);
            await AssertMemberProblemAsync(escalation, HttpStatusCode.Forbidden, "membership_management_forbidden");
        }

        var ownerId = (await GetMeAsync(owner)).UserId;
        var ownerMember = await ReadMemberAsync(owner, workspace.Id, ownerId);
        foreach (var target in new[] { adminMember, ownerMember })
        {
            using var roleDenied = await SendMemberChangeAsync(admin, workspace.Id, target.UserId, "Viewer", target.Version);
            using var deactivationDenied = await SendMemberChangeAsync(admin, workspace.Id, target.UserId, null, target.Version);
            Assert.AreEqual(HttpStatusCode.Forbidden, roleDenied.StatusCode);
            Assert.AreEqual(HttpStatusCode.Forbidden, deactivationDenied.StatusCode);
        }

        await DeactivateMemberAsync(admin, workspace.Id, ordinary);
        // An owner can demote an administrator; their unchanged token cannot invite afterwards.
        await ChangeMemberRoleAsync(owner, workspace.Id, adminMember, "Viewer");
        using var staleAuthority = await admin.PostAsJsonAsync(new Uri($"/api/v1/workspaces/{workspace.Id:D}/invitations", UriKind.Relative),
            new InviteWorkspaceMemberRequest($"functional|not-invited-{suffix}", "Not Invited", "Viewer"));
        Assert.AreEqual(HttpStatusCode.Forbidden, staleAuthority.StatusCode);
    }

    [TestMethod]
    public async Task Membership_last_owner_is_protected_until_another_active_owner_exists()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var owner = CreateAuthorizedClient($"functional|last-owner-{suffix}", "Owner");
        using var successor = CreateAuthorizedClient($"functional|successor-{suffix}", "Successor");
        var workspace = await CreateWorkspaceAsync(owner, "Ownership Workspace");
        var ownerMember = await ReadMemberAsync(owner, workspace.Id, (await GetMeAsync(owner)).UserId);
        using var demotion = await SendMemberChangeAsync(owner, workspace.Id, ownerMember.UserId, "Administrator", ownerMember.Version);
        using var deactivation = await SendMemberChangeAsync(owner, workspace.Id, ownerMember.UserId, null, ownerMember.Version);
        await AssertMemberProblemAsync(demotion, HttpStatusCode.Conflict, "last_workspace_owner");
        await AssertMemberProblemAsync(deactivation, HttpStatusCode.Conflict, "last_workspace_owner");
        var invited = await InviteMemberAsync(owner, workspace.Id, $"functional|successor-{suffix}", "Successor", WorkspaceRole.Viewer);
        var promoted = await ChangeMemberRoleAsync(owner, workspace.Id, invited, "Owner");
        await DeactivateMemberAsync(owner, workspace.Id, ownerMember);
        // Inactive former owners must not count toward the last-owner invariant.
        using var last = await SendMemberChangeAsync(successor, workspace.Id, promoted.UserId, "Viewer", promoted.Version);
        await AssertMemberProblemAsync(last, HttpStatusCode.Conflict, "last_workspace_owner");
    }

    [TestMethod]
    public async Task Membership_mutations_deny_non_managers_and_do_not_disclose_foreign_targets()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var owner = CreateAuthorizedClient($"functional|boundary-owner-{suffix}", "Owner");
        using var outsider = CreateAuthorizedClient($"functional|boundary-outsider-{suffix}", "Outsider");
        using var viewer = CreateAuthorizedClient($"functional|boundary-viewer-{suffix}", "Viewer");
        using var contributor = CreateAuthorizedClient($"functional|boundary-contributor-{suffix}", "Contributor");
        using var anonymous = CreateClient();
        var workspace = await CreateWorkspaceAsync(owner, "Boundary Workspace");
        var foreign = await CreateWorkspaceAsync(outsider, "Foreign Workspace");
        var target = await InviteMemberAsync(owner, workspace.Id, $"functional|boundary-viewer-{suffix}", "Viewer", WorkspaceRole.Viewer);
        await InviteMemberAsync(owner, workspace.Id, $"functional|boundary-contributor-{suffix}", "Contributor", WorkspaceRole.ProjectContributor);
        var foreignOwner = await ReadMemberAsync(outsider, foreign.Id, (await GetMeAsync(outsider)).UserId);
        foreach (var (client, status) in new[] { (viewer, HttpStatusCode.Forbidden), (contributor, HttpStatusCode.Forbidden),
                     (outsider, HttpStatusCode.NotFound), (anonymous, HttpStatusCode.Unauthorized) })
        {
            foreach (var role in new string?[] { "Owner", null })
            {
                using var denied = await SendMemberChangeAsync(client, workspace.Id, target.UserId, role, target.Version);
                Assert.AreEqual(status, denied.StatusCode);
            }
        }

        foreach (var role in new string?[] { "Viewer", null })
        {
            using var foreignTarget = await SendMemberChangeAsync(owner, workspace.Id, foreignOwner.UserId, role, foreignOwner.Version);
            using var missingTarget = await SendMemberChangeAsync(owner, workspace.Id, Guid.NewGuid(), role, foreignOwner.Version);
            using var foreignWorkspace = await SendMemberChangeAsync(owner, foreign.Id, foreignOwner.UserId, role, foreignOwner.Version);
            Assert.AreEqual(HttpStatusCode.NotFound, foreignTarget.StatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, missingTarget.StatusCode);
            Assert.AreEqual(await missingTarget.Content.ReadAsStringAsync(), await foreignTarget.Content.ReadAsStringAsync());
            Assert.AreEqual(HttpStatusCode.NotFound, foreignWorkspace.StatusCode);
        }

        Assert.AreEqual(foreignOwner, await ReadMemberAsync(outsider, foreign.Id, foreignOwner.UserId));
        Assert.AreEqual(target, await ReadMemberAsync(owner, workspace.Id, target.UserId));
    }

    [TestMethod]
    public async Task Membership_inputs_reject_missing_numeric_and_malformed_values_without_mutation()
    {
        using var owner = CreateAuthorizedClient($"functional|input-owner-{Guid.NewGuid():N}", "Owner");
        var workspace = await CreateWorkspaceAsync(owner, "Input Workspace");
        var member = await InviteMemberAsync(owner, workspace.Id, $"functional|input-member-{Guid.NewGuid():N}", "Member", WorkspaceRole.Viewer);
        foreach (var role in new string?[] { "1", "2", "99", "Owner,Viewer", "Unknown", "<owner>", "", null })
        {
            using var response = await owner.PatchAsJsonAsync(new Uri($"/api/v1/workspaces/{workspace.Id:D}/members/{member.UserId:D}/role", UriKind.Relative),
                new { role, expectedVersion = member.Version });
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        foreach (var version in new string?[] { "", null, "123", " 0000001", "0000001 ", "FFFFFFFFF", "００000001", "ZZ000001" })
        {
            foreach (var role in new string?[] { "Owner", null })
            {
                using var response = await SendMemberChangeAsync(owner, workspace.Id, member.UserId, role, version!);
                await AssertMemberProblemAsync(response, HttpStatusCode.UnprocessableEntity, "invalid_membership_version");
            }
        }

        using var missing = await owner.PatchAsJsonAsync(new Uri($"/api/v1/workspaces/{workspace.Id:D}/members/{member.UserId:D}/role", UriKind.Relative), new { role = "Owner" });
        await AssertMemberProblemAsync(missing, HttpStatusCode.UnprocessableEntity, "invalid_membership_version");
        using var emptyId = await SendMemberChangeAsync(owner, workspace.Id, Guid.Empty, null, member.Version);
        await AssertMemberProblemAsync(emptyId, HttpStatusCode.UnprocessableEntity, "invalid_member_id");
        foreach (var numericRole in new[] { "1", "2", "3", "4" })
        {
            using var invalidInvitation = await owner.PostAsJsonAsync(new Uri($"/api/v1/workspaces/{workspace.Id:D}/invitations", UriKind.Relative),
                new InviteWorkspaceMemberRequest($"functional|numeric-{Guid.NewGuid():N}", "Numeric Role", numericRole));
            await AssertMemberProblemAsync(invalidInvitation, HttpStatusCode.UnprocessableEntity, "invalid_membership_role");
        }

        Assert.AreEqual(member, await ReadMemberAsync(owner, workspace.Id, member.UserId));
    }

    private static async Task<WorkspaceMemberResponse> ReadMemberAsync(HttpClient client, Guid workspaceId, Guid userId)
    {
        using var response = await client.GetAsync(new Uri($"/api/v1/workspaces/{workspaceId:D}/members", UriKind.Relative));
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var members = await response.Content.ReadFromJsonAsync<WorkspaceMemberResponse[]>();
        Assert.IsNotNull(members);
        return members.Single(member => member.UserId == userId);
    }

    private static async Task<WorkspaceMemberResponse> ChangeMemberRoleAsync(HttpClient client, Guid workspaceId, WorkspaceMemberResponse member, string role)
    {
        using var response = await SendMemberChangeAsync(client, workspaceId, member.UserId, role, member.Version);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<WorkspaceMemberResponse>();
        Assert.IsNotNull(result);
        return result;
    }

    private static async Task<WorkspaceMemberResponse> DeactivateMemberAsync(HttpClient client, Guid workspaceId, WorkspaceMemberResponse member)
    {
        using var response = await SendMemberChangeAsync(client, workspaceId, member.UserId, null, member.Version);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<WorkspaceMemberResponse>();
        Assert.IsNotNull(result);
        return result;
    }

    private static Task<HttpResponseMessage> SendMemberChangeAsync(HttpClient client, Guid workspaceId, Guid userId, string? role, string version) =>
        role is null
            ? client.PostAsJsonAsync(new Uri($"/api/v1/workspaces/{workspaceId:D}/members/{userId:D}/deactivation", UriKind.Relative),
                new DeactivateWorkspaceMemberRequest(version))
            : client.PatchAsJsonAsync(new Uri($"/api/v1/workspaces/{workspaceId:D}/members/{userId:D}/role", UriKind.Relative),
                new ChangeWorkspaceMemberRoleRequest(role, version));

    private static async Task AssertMemberProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.AreEqual(status, response.StatusCode, await response.Content.ReadAsStringAsync());
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual(code, problem.RootElement.GetProperty("code").GetString());
    }
}
