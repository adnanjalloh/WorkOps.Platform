using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOps.Domain;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces");
        builder.HasKey(workspace => workspace.Id);
        builder.Property(workspace => workspace.Id)
            .HasConversion(id => id.Value, value => WorkspaceId.From(value));
        builder.Property(workspace => workspace.Name).HasMaxLength(120).IsRequired();
        builder.Property(workspace => workspace.Slug).HasMaxLength(64).IsRequired();
        builder.Property(workspace => workspace.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.HasIndex(workspace => workspace.Slug)
            .IsUnique()
            .HasDatabaseName("UX_workspaces_slug");
    }
}
