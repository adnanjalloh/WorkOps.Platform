using WorkOps.Application.Tenancy;
using WorkOps.Domain;
using WorkOps.Domain.Tenancy;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class WorkspaceMembershipTests
{
    [TestMethod]
    public void Role_changes_preserve_identity_and_no_ops_preserve_timestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var member = WorkspaceMembership.Create(WorkspaceId.New(), Guid.NewGuid(), WorkspaceRole.Viewer, now);
        var workspaceId = member.WorkspaceId;
        var userId = member.UserId;
        member.ChangeRole(WorkspaceRole.Administrator, now.AddMinutes(1));
        member.ChangeRole(WorkspaceRole.Administrator, now.AddMinutes(2));
        Assert.AreEqual(WorkspaceRole.Administrator, member.Role);
        Assert.AreEqual(now.AddMinutes(1), member.UpdatedAt);
        Assert.AreEqual(workspaceId, member.WorkspaceId);
        Assert.AreEqual(userId, member.UserId);
        Assert.AreEqual(now, member.CreatedAt);
        Assert.IsTrue(member.IsActive);
    }

    [TestMethod]
    public void Deactivation_is_repeatable_but_does_not_allow_role_changes_or_reactivation()
    {
        var now = DateTimeOffset.UtcNow;
        var member = WorkspaceMembership.Create(WorkspaceId.New(), Guid.NewGuid(), WorkspaceRole.Owner, now);
        member.Deactivate(now.AddMinutes(1));
        member.Deactivate(now.AddMinutes(2));
        Assert.IsFalse(member.IsActive);
        Assert.AreEqual(now.AddMinutes(1), member.UpdatedAt);
        Assert.ThrowsExactly<InactiveWorkspaceMembershipException>(
            () => member.ChangeRole(WorkspaceRole.Viewer, now.AddMinutes(3)));
        Assert.AreEqual(WorkspaceRole.Owner, member.Role);
    }

    [TestMethod]
    public void Undefined_roles_are_rejected_without_mutation()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WorkspaceMembership.Create(WorkspaceId.New(), Guid.NewGuid(), (WorkspaceRole)0, now));
        var member = WorkspaceMembership.Create(WorkspaceId.New(), Guid.NewGuid(), WorkspaceRole.Viewer, now);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => member.ChangeRole((WorkspaceRole)99, now));
        Assert.AreEqual(WorkspaceRole.Viewer, member.Role);
        Assert.IsNull(member.UpdatedAt);
    }

    [TestMethod]
    public void Membership_version_round_trips_without_normalization()
    {
        Assert.AreEqual("00ABC123", MembershipVersion.Encode(0x00ABC123));
        Assert.IsTrue(MembershipVersion.TryDecode("00abc123", out var version));
        Assert.AreEqual(0x00ABC123u, version);
        foreach (var invalid in new string?[] { null, "", "ABC123", " 0ABC123", "0ABC123 ", "００ABC123", "GGABC123", "000000000", "\n0ABC123" })
        {
            Assert.IsFalse(MembershipVersion.TryDecode(invalid, out _));
        }
    }
}
