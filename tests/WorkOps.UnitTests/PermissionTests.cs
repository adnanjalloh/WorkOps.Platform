using WorkOps.Domain.Tenancy;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class PermissionTests
{
    [TestMethod]
    public void Owner_has_every_defined_permission()
    {
        var permissions = Permissions.ForRole(WorkspaceRole.Owner);

        CollectionAssert.AreEquivalent(
            new[]
            {
                Permissions.WorkspacesRead,
                Permissions.WorkspacesManage,
                Permissions.MembersRead,
                Permissions.MembersManage,
                Permissions.ProjectsRead,
                Permissions.ProjectsWrite,
                Permissions.AuditRead,
            },
            permissions.ToArray());
    }

    [TestMethod]
    public void Viewer_is_read_only()
    {
        var permissions = Permissions.ForRole(WorkspaceRole.Viewer);

        CollectionAssert.AreEquivalent(
            new[]
            {
                Permissions.WorkspacesRead,
                Permissions.MembersRead,
                Permissions.ProjectsRead,
            },
            permissions.ToArray());
        CollectionAssert.DoesNotContain(permissions.ToArray(), Permissions.ProjectsWrite);
    }

    [TestMethod]
    public void Unknown_role_has_no_permissions()
    {
        Assert.IsEmpty(Permissions.ForRole((WorkspaceRole)999));
    }
}
