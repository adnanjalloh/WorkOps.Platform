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

    [TestMethod]
    public void Background_context_sets_only_the_tenant_boundary()
    {
        var accessor = new WorkspaceContextAccessor();
        var workspaceId = WorkspaceId.New();

        accessor.EstablishBackground(workspaceId);

        Assert.AreEqual(workspaceId, accessor.CurrentWorkspaceId);
        Assert.IsNull(accessor.Current);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => accessor.EstablishBackground(WorkspaceId.New()));
    }
}
