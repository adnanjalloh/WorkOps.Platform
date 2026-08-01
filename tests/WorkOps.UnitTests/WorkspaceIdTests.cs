using WorkOps.Domain;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class WorkspaceIdTests
{
    [TestMethod]
    public void New_creates_non_empty_unique_identifiers()
    {
        var first = WorkspaceId.New();
        var second = WorkspaceId.New();

        Assert.AreNotEqual(Guid.Empty, first.Value);
        Assert.AreNotEqual(first, second);
    }
}
