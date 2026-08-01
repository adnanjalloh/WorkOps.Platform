using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOps.Domain;
using WorkOps.Domain.Messaging;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Infrastructure.Persistence.Configurations;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(message => new { message.WorkspaceId, message.MessageId, message.Consumer })
            .HasName("PK_inbox_messages");
        builder.Property(message => message.WorkspaceId)
            .HasConversion(id => id.Value, value => WorkspaceId.From(value));
        builder.Property(message => message.Consumer).HasMaxLength(128);

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(message => message.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<OutboxMessage>()
            .WithMany()
            .HasForeignKey(message => new { message.WorkspaceId, Id = message.MessageId })
            .HasPrincipalKey(outbox => new { outbox.WorkspaceId, outbox.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
