using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOps.Domain;
using WorkOps.Domain.Projects;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(project => project.Id);
        builder.HasAlternateKey(project => new { project.WorkspaceId, project.Id });
        builder.Property(project => project.WorkspaceId)
            .HasConversion(id => id.Value, value => WorkspaceId.From(value));
        builder.Property(project => project.Name).HasMaxLength(120).IsRequired();
        builder.Property(project => project.Key).HasMaxLength(64).IsRequired();
        builder.Property(project => project.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(project => project.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(project => new { project.WorkspaceId, project.Key })
            .IsUnique()
            .HasDatabaseName("UX_projects_workspace_key");
        builder.HasIndex(project => new { project.WorkspaceId, project.Status, project.Name });
    }
}
