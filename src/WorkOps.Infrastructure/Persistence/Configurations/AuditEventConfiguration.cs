using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOps.Domain;
using WorkOps.Domain.Audit;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.WorkspaceId)
            .HasConversion(id => id.Value, value => WorkspaceId.From(value));
        builder.Property(auditEvent => auditEvent.Action).HasMaxLength(64).IsRequired();
        builder.Property(auditEvent => auditEvent.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(auditEvent => auditEvent.CorrelationId).HasMaxLength(128).IsRequired();
        builder.Property(auditEvent => auditEvent.MetadataJson).HasColumnType("jsonb").IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(auditEvent => auditEvent.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkspaceMembership>()
            .WithMany()
            .HasForeignKey(auditEvent => new { auditEvent.WorkspaceId, auditEvent.ActorUserId })
            .HasPrincipalKey(membership => new { membership.WorkspaceId, membership.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(auditEvent => new
        {
            auditEvent.WorkspaceId,
            auditEvent.OccurredAt,
            auditEvent.Id,
        });
        builder.HasIndex(auditEvent => new
        {
            auditEvent.WorkspaceId,
            auditEvent.Action,
            auditEvent.OccurredAt,
        });
    }
}
