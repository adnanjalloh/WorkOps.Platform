using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOps.Domain;
using WorkOps.Domain.Identity;
using WorkOps.Domain.Projects;
using WorkOps.Domain.WorkItems;

namespace WorkOps.Infrastructure.Persistence.Configurations;

internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("work_items");
        builder.HasKey(workItem => workItem.Id);
        builder.Property(workItem => workItem.WorkspaceId)
            .HasConversion(id => id.Value, value => WorkspaceId.From(value));
        builder.Property(workItem => workItem.Title).HasMaxLength(120).IsRequired();
        builder.Property(workItem => workItem.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(workItem => workItem.Priority)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(workItem => workItem.Labels)
            .HasColumnType("text[]")
            .IsRequired();
        builder.Property(workItem => workItem.Version).IsRowVersion();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(workItem => new { workItem.WorkspaceId, workItem.ProjectId })
            .HasPrincipalKey(project => new { project.WorkspaceId, project.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(workItem => workItem.AssigneeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(workItem => new
        {
            workItem.WorkspaceId,
            workItem.ProjectId,
            workItem.Status,
        });
        builder.HasIndex(workItem => new
        {
            workItem.WorkspaceId,
            workItem.AssigneeUserId,
            workItem.Status,
        });
    }
}
