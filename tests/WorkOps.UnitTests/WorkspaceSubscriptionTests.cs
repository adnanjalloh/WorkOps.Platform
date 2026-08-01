using WorkOps.Domain;
using WorkOps.Domain.Features;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class WorkspaceSubscriptionTests
{
    [TestMethod]
    public void Starter_plan_enforces_two_active_projects()
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = WorkspaceSubscription.CreateStarter(WorkspaceId.New(), now);

        subscription.ReserveProjectSlot(2, now.AddSeconds(1));
        subscription.ReserveProjectSlot(2, now.AddSeconds(2));

        Assert.AreEqual(2, subscription.ActiveProjectCount);
        Assert.ThrowsExactly<FeatureLimitExceededException>(
            () => subscription.ReserveProjectSlot(2, now.AddSeconds(3)));
    }

    [TestMethod]
    public void Released_slot_can_be_reserved_again()
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = WorkspaceSubscription.CreateStarter(WorkspaceId.New(), now);
        subscription.ReserveProjectSlot(2, now);
        subscription.ReserveProjectSlot(2, now);

        subscription.ReleaseProjectSlot(now.AddSeconds(1));
        subscription.ReserveProjectSlot(2, now.AddSeconds(2));

        Assert.AreEqual(2, subscription.ActiveProjectCount);
    }

    [TestMethod]
    public void Plan_change_is_explicit_and_idempotent()
    {
        var subscription = WorkspaceSubscription.CreateStarter(
            WorkspaceId.New(),
            DateTimeOffset.UtcNow);

        Assert.IsTrue(subscription.ChangePlan(WorkspacePlan.Team, DateTimeOffset.UtcNow));
        Assert.IsFalse(subscription.ChangePlan(WorkspacePlan.Team, DateTimeOffset.UtcNow));
        Assert.AreEqual(WorkspacePlan.Team, subscription.Plan);
    }
}
