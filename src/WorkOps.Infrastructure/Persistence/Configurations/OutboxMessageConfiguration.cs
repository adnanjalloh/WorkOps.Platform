using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOps.Domain;
using WorkOps.Domain.Messaging;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.HasAlternateKey(message => new { message.WorkspaceId, message.Id });
        builder.Property(message => message.WorkspaceId)
            .HasConversion(id => id.Value, value => WorkspaceId.From(value));
        builder.Property(message => message.Type).HasMaxLength(128).IsRequired();
        builder.Property(message => message.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(message => message.LastErrorCode).HasMaxLength(128);

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(message => message.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(message => new
        {
            message.Status,
            message.NextAttemptAt,
            message.OccurredAt,
        });
        builder.HasIndex(message => new
        {
            message.WorkspaceId,
            message.Status,
            message.OccurredAt,
        });
    }
}
