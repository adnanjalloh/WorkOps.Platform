using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WorkOps.FunctionalTests;

[TestClass]
public sealed class HealthEndpointTests
{
    [TestMethod]
    public async Task Live_endpoint_returns_healthy()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
