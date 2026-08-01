using Microsoft.EntityFrameworkCore;
using Npgsql;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Common.Pagination;
using WorkOps.Application.Messaging;
using WorkOps.Application.Notifications;
using WorkOps.Domain;
using WorkOps.Domain.Messaging;
using WorkOps.Domain.Notifications;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Notifications;

internal sealed class NotificationStore(WorkOpsDbContext dbContext) : INotificationStore
{
    private const string ConsumerName = "development-notification-channel.v1";

    public async Task<bool> TryDeliverAsync(
        WorkItemStatusChangedMessage message,
        DateTimeOffset deliveredAt,
        CancellationToken cancellationToken)
    {
        var workspaceId = WorkspaceId.From(message.WorkspaceId);
        if (await dbContext.InboxMessages.AnyAsync(
                inbox => inbox.MessageId == message.MessageId && inbox.Consumer == ConsumerName,
                cancellationToken))
        {
            return false;
        }

        dbContext.InboxMessages.Add(InboxMessage.Record(
            workspaceId,
            message.MessageId,
            ConsumerName,
            deliveredAt));
        dbContext.NotificationDeliveries.Add(NotificationDelivery.Create(
            workspaceId,
            message.MessageId,
            message.RecipientUserId,
            "development",
            "work_item_status_changed",
            "work_item",
            message.WorkItemId,
            deliveredAt));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "PK_inbox_messages" or
                    "UX_notification_deliveries_deduplication",
            })
        {
            return false;
        }
    }

    public async Task<PagedResult<NotificationView>> ListAsync(
        Guid recipientUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.NotificationDeliveries
            .AsNoTracking()
            .Where(delivery => delivery.RecipientUserId == recipientUserId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(delivery => delivery.CreatedAt)
            .ThenByDescending(delivery => delivery.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(delivery => new NotificationView(
                delivery.Id,
                delivery.SourceMessageId,
                delivery.Channel,
                delivery.Template,
                delivery.EntityType,
                delivery.EntityId,
                delivery.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<NotificationView>(items, page, pageSize, totalCount);
    }
}
