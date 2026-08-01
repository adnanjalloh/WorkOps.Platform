using WorkOps.Api.Tenancy;
using WorkOps.Application.Notifications;
using WorkOps.Contracts.Common;
using WorkOps.Contracts.Notifications;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Endpoints;

internal static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/notifications", ListAsync)
            .RequireAuthorization(Permissions.NotificationsRead)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Header))
            .WithName("ListNotifications");
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        NotificationService notificationService,
        CancellationToken cancellationToken,
        [SkipSanitization(Reason = "The query value is parsed as an integer and range validated before use.")]
        int page = 1,
        [SkipSanitization(Reason = "The query value is parsed as an integer and range validated before use.")]
        int pageSize = 20)
    {
        var result = await notificationService.ListAsync(page, pageSize, cancellationToken);
        var items = result.Items.Select(notification => new NotificationResponse(
            notification.Id,
            notification.SourceMessageId,
            notification.Channel,
            notification.Template,
            notification.EntityType,
            notification.EntityId,
            notification.CreatedAt))
            .ToArray();
        return Results.Ok(new PagedResponse<NotificationResponse>(
            items,
            result.Page,
            result.PageSize,
            result.TotalCount));
    }
}
