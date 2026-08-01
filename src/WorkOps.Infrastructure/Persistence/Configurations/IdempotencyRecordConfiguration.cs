using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOps.Domain;
using WorkOps.Domain.Idempotency;
using WorkOps.Domain.Identity;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(record => new
        {
            record.WorkspaceId,
            record.UserId,
            record.Method,
            record.Route,
            record.Key,
        });
        builder.Property(record => record.WorkspaceId)
            .HasConversion(id => id.Value, value => WorkspaceId.From(value));
        builder.Property(record => record.Method).HasMaxLength(8);
        builder.Property(record => record.Route).HasMaxLength(160);
        builder.Property(record => record.Key).HasMaxLength(200);
        builder.Property(record => record.RequestHash).HasMaxLength(64).IsFixedLength();
        builder.Property(record => record.ResponseBodyJson).HasColumnType("jsonb");

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(record => record.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(record => record.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkspaceMembership>()
            .WithMany()
            .HasForeignKey(record => new { record.WorkspaceId, record.UserId })
            .HasPrincipalKey(membership => new { membership.WorkspaceId, membership.UserId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(record => record.ExpiresAt);
    }
}
