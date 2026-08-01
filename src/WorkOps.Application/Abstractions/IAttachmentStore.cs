using WorkOps.Application.Files;
using WorkOps.Domain.Files;

namespace WorkOps.Application.Abstractions;

public interface IAttachmentStore
{
    void Add(Attachment attachment);

    Task<Attachment?> FindAsync(Guid attachmentId, CancellationToken cancellationToken);

    Task<AttachmentView?> GetAsync(Guid attachmentId, CancellationToken cancellationToken);
}
