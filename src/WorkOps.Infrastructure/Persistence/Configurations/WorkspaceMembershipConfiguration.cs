using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOps.Domain;
using WorkOps.Domain.Identity;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceMembershipConfiguration : IEntityTypeConfiguration<WorkspaceMembership>
{
    public void Configure(EntityTypeBuilder<WorkspaceMembership> builder)
    {
        builder.ToTable("workspace_memberships");
        builder.HasKey(membership => new { membership.WorkspaceId, membership.UserId })
            .HasName("PK_workspace_memberships");
        builder.Property(membership => membership.WorkspaceId)
            .HasConversion(id => id.Value, value => WorkspaceId.From(value));
        builder.Property(membership => membership.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(membership => membership.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(membership => new { membership.UserId, membership.IsActive });
    }
}
