using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Files;
using WorkOps.Domain.Files;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Files;

internal sealed class AttachmentStore(WorkOpsDbContext dbContext) : IAttachmentStore
{
    public void Add(Attachment attachment) => dbContext.Attachments.Add(attachment);

    public Task<Attachment?> FindAsync(
        Guid attachmentId,
        CancellationToken cancellationToken) => dbContext.Attachments
        .AsNoTracking()
        .SingleOrDefaultAsync(attachment => attachment.Id == attachmentId, cancellationToken);

    public Task<AttachmentView?> GetAsync(
        Guid attachmentId,
        CancellationToken cancellationToken) => dbContext.Attachments
        .AsNoTracking()
        .Where(attachment => attachment.Id == attachmentId)
        .Select(attachment => new AttachmentView(
            attachment.Id,
            attachment.WorkItemId,
            attachment.FileName,
            attachment.ContentType,
            attachment.Size,
            attachment.Sha256,
            attachment.CreatedAt))
        .SingleOrDefaultAsync(cancellationToken);
}
