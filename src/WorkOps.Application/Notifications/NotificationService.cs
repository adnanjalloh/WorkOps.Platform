using WorkOps.Application.Abstractions;
using WorkOps.Application.Common.Pagination;
using WorkOps.Application.Common.Validation;
using WorkOps.Application.Tenancy;

namespace WorkOps.Application.Notifications;

public sealed class NotificationService(
    INotificationStore notifications,
    IWorkspaceContextAccessor workspaceContext)
{
    public Task<PagedResult<NotificationView>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page is < 1 or > 10_000 || pageSize is < 1 or > 100)
        {
            throw new RequestValidationException("invalid_pagination");
        }

        var current = workspaceContext.Current
            ?? throw new InvalidOperationException("An interactive workspace context is required.");
        return notifications.ListAsync(current.UserId, page, pageSize, cancellationToken);
    }
}
