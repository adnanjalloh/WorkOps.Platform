using WorkOps.Application.WorkItems;
using WorkOps.Domain;
using WorkOps.Domain.WorkItems;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class WorkItemTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly string[] UpdatedLabels = ["backend", "release"];

    [TestMethod]
    public void Supported_transition_path_reaches_completed()
    {
        var workItem = CreateWorkItem();

        workItem.TransitionTo(WorkItemStatus.InProgress, CreatedAt.AddMinutes(1));
        workItem.TransitionTo(WorkItemStatus.Blocked, CreatedAt.AddMinutes(2));
        workItem.TransitionTo(WorkItemStatus.InProgress, CreatedAt.AddMinutes(3));
        workItem.TransitionTo(WorkItemStatus.Completed, CreatedAt.AddMinutes(4));

        Assert.AreEqual(WorkItemStatus.Completed, workItem.Status);
        Assert.AreEqual(CreatedAt.AddMinutes(4), workItem.UpdatedAt);
    }

    [TestMethod]
    public void Unsupported_transition_does_not_change_state()
    {
        var workItem = CreateWorkItem();

        Assert.ThrowsExactly<InvalidWorkItemTransitionException>(
            () => workItem.TransitionTo(WorkItemStatus.Completed, CreatedAt.AddMinutes(1)));
        Assert.AreEqual(WorkItemStatus.Backlog, workItem.Status);
        Assert.IsNull(workItem.UpdatedAt);
    }

    [TestMethod]
    public void Completed_work_item_is_terminal()
    {
        var workItem = CreateWorkItem();
        workItem.TransitionTo(WorkItemStatus.InProgress, CreatedAt.AddMinutes(1));
        workItem.TransitionTo(WorkItemStatus.Completed, CreatedAt.AddMinutes(2));

        Assert.ThrowsExactly<InvalidWorkItemTransitionException>(
            () => workItem.TransitionTo(WorkItemStatus.InProgress, CreatedAt.AddMinutes(3)));
    }

    [TestMethod]
    public void Update_replaces_assignment_priority_and_labels()
    {
        var workItem = CreateWorkItem();
        var assignee = Guid.NewGuid();
        var updatedAt = CreatedAt.AddMinutes(1);

        workItem.UpdateDetails(
            "Ship API",
            WorkItemPriority.Critical,
            assignee,
            ["backend", "release"],
            updatedAt);

        Assert.AreEqual("Ship API", workItem.Title);
        Assert.AreEqual(WorkItemPriority.Critical, workItem.Priority);
        Assert.AreEqual(assignee, workItem.AssigneeUserId);
        CollectionAssert.AreEqual(UpdatedLabels, workItem.Labels);
        Assert.AreEqual(updatedAt, workItem.UpdatedAt);
    }

    [TestMethod]
    public void Concurrency_token_round_trips_as_fixed_width_hex()
    {
        const uint version = 0x00ABC123;

        var encoded = WorkItemVersion.Encode(version);
        var decoded = WorkItemVersion.TryDecode(encoded, out var parsed);

        Assert.AreEqual("00ABC123", encoded);
        Assert.IsTrue(decoded);
        Assert.AreEqual(version, parsed);
        Assert.IsFalse(WorkItemVersion.TryDecode("ABC123", out _));
    }

    private static WorkItem CreateWorkItem() => WorkItem.Create(
        WorkspaceId.New(),
        Guid.NewGuid(),
        "Build API",
        WorkItemPriority.Normal,
        null,
        ["backend"],
        CreatedAt);
}
