using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOps.Domain;
using WorkOps.Domain.Features;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceSubscriptionConfiguration : IEntityTypeConfiguration<WorkspaceSubscription>
{
    public void Configure(EntityTypeBuilder<WorkspaceSubscription> builder)
    {
        builder.ToTable("workspace_subscriptions");
        builder.HasKey(subscription => subscription.WorkspaceId);
        builder.Property(subscription => subscription.WorkspaceId)
            .HasConversion(id => id.Value, value => WorkspaceId.From(value));
        builder.Property(subscription => subscription.Plan)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(subscription => subscription.Version).IsRowVersion();

        builder.HasOne<Workspace>()
            .WithOne()
            .HasForeignKey<WorkspaceSubscription>(subscription => subscription.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
