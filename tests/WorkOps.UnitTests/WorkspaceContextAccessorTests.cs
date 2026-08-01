using WorkOps.Application.Tenancy;
using WorkOps.Domain;
using WorkOps.Domain.Tenancy;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class WorkspaceContextAccessorTests
{
    [TestMethod]
    public void Context_can_only_be_established_once_per_scope()
    {
        var accessor = new WorkspaceContextAccessor();
        var context = new WorkspaceContext(
            Guid.NewGuid(),
            WorkspaceId.New(),
            WorkspaceRole.Owner,
            WorkspaceStatus.Active);

        accessor.Establish(context);

        Assert.AreSame(context, accessor.Current);
        Assert.ThrowsExactly<InvalidOperationException>(() => accessor.Establish(context));
    }
}
