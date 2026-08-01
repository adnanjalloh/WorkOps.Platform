using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOps.Domain;
using WorkOps.Domain.Messaging;
using WorkOps.Domain.Notifications;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Infrastructure.Persistence.Configurations;

internal sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_deliveries");
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.WorkspaceId)
            .HasConversion(id => id.Value, value => WorkspaceId.From(value));
        builder.Property(delivery => delivery.Channel).HasMaxLength(64).IsRequired();
        builder.Property(delivery => delivery.Template).HasMaxLength(128).IsRequired();
        builder.Property(delivery => delivery.EntityType).HasMaxLength(64).IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(delivery => delivery.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkspaceMembership>()
            .WithMany()
            .HasForeignKey(delivery => new { delivery.WorkspaceId, UserId = delivery.RecipientUserId })
            .HasPrincipalKey(membership => new { membership.WorkspaceId, membership.UserId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OutboxMessage>()
            .WithMany()
            .HasForeignKey(delivery => new { delivery.WorkspaceId, Id = delivery.SourceMessageId })
            .HasPrincipalKey(outbox => new { outbox.WorkspaceId, outbox.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(delivery => new
        {
            delivery.WorkspaceId,
            delivery.SourceMessageId,
            delivery.RecipientUserId,
            delivery.Channel,
        })
            .IsUnique()
            .HasDatabaseName("UX_notification_deliveries_deduplication");
        builder.HasIndex(delivery => new
        {
            delivery.WorkspaceId,
            delivery.RecipientUserId,
            delivery.CreatedAt,
        });
    }
}
