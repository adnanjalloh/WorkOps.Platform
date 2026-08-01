using WorkOps.Domain;
using WorkOps.Domain.Projects;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class ProjectTests
{
    [TestMethod]
    public void Archive_is_idempotent_and_records_update_time()
    {
        var createdAt = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddHours(1);
        var project = Project.Create(WorkspaceId.New(), "Delivery", "delivery", createdAt);

        var firstArchive = project.Archive(updatedAt);
        var secondArchive = project.Archive(updatedAt.AddMinutes(1));

        Assert.IsTrue(firstArchive);
        Assert.IsFalse(secondArchive);
        Assert.AreEqual(ProjectStatus.Archived, project.Status);
        Assert.AreEqual(updatedAt, project.UpdatedAt);
    }
}
