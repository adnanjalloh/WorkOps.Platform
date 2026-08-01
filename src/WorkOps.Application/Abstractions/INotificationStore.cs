using WorkOps.Application.Common.Pagination;
using WorkOps.Application.Messaging;
using WorkOps.Application.Notifications;

namespace WorkOps.Application.Abstractions;

public interface INotificationStore
{
    Task<bool> TryDeliverAsync(
        WorkItemStatusChangedMessage message,
        DateTimeOffset deliveredAt,
        CancellationToken cancellationToken);

    Task<PagedResult<NotificationView>> ListAsync(
        Guid recipientUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
