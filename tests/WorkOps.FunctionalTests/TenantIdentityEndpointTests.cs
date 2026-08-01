using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using WorkOps.Contracts.Identity;
using WorkOps.Contracts.Tenancy;
using WorkOps.Domain.Tenancy;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.FunctionalTests;

[TestClass]
public sealed class TenantIdentityEndpointTests
{
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
            var suspendedWorkspace = await dbContext.Workspaces
                .IgnoreQueryFilters()
                .SingleAsync(workspace => workspace.Id == WorkOps.Domain.WorkspaceId.From(suspended.Id));
            var inactiveMembership = await dbContext.WorkspaceMemberships
                .IgnoreQueryFilters()
                .SingleAsync(membership => membership.WorkspaceId == WorkOps.Domain.WorkspaceId.From(inactive.Id));

            suspendedWorkspace.Suspend(DateTimeOffset.UtcNow);
            inactiveMembership.Deactivate(DateTimeOffset.UtcNow);
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
    {
        var token = new JwtSecurityToken(
            issuer: WorkOpsWebApplicationFactory.Issuer,
            audience: audience,
            claims:
            [
                new Claim("sub", subject),
                new Claim("name", displayName),
            ],
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
}
