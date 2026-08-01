using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOps.Domain;
using WorkOps.Domain.Files;
using WorkOps.Domain.Tenancy;
using WorkOps.Domain.WorkItems;

namespace WorkOps.Infrastructure.Persistence.Configurations;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.WorkspaceId)
            .HasConversion(id => id.Value, value => WorkspaceId.From(value));
        builder.Property(attachment => attachment.FileName).HasMaxLength(120).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(attachment => attachment.StorageName).HasMaxLength(64).IsRequired();
        builder.Property(attachment => attachment.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();

        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(attachment => new { attachment.WorkspaceId, attachment.WorkItemId })
            .HasPrincipalKey(workItem => new { workItem.WorkspaceId, workItem.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkspaceMembership>()
            .WithMany()
            .HasForeignKey(attachment => new
            {
                attachment.WorkspaceId,
                UserId = attachment.UploadedByUserId,
            })
            .HasPrincipalKey(membership => new { membership.WorkspaceId, membership.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(attachment => new
        {
            attachment.WorkspaceId,
            attachment.WorkItemId,
            attachment.CreatedAt,
        });
        builder.HasIndex(attachment => new
        {
            attachment.WorkspaceId,
            attachment.StorageName,
        }).IsUnique();
    }
}
