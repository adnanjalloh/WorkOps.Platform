namespace WorkOps.IntegrationTests;

[TestClass]
public sealed class FoundationSmokeTests
{
    [TestMethod]
    public void Infrastructure_assembly_has_expected_identity()
    {
        var assemblyName = typeof(Infrastructure.AssemblyMarker).Assembly.GetName().Name;

        Assert.AreEqual("WorkOps.Infrastructure", assemblyName);
    }
}
