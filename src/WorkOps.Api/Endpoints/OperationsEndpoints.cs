using WorkOps.Api.Tenancy;
using WorkOps.Application.Messaging;
using WorkOps.Contracts.Common;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Endpoints;

internal static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/operations/outbox/{messageId:guid}/replay", ReplayAsync)
            .RequireAuthorization(Permissions.OperationsManage)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Header))
            .WithName("ReplayFailedOutboxMessage");
        return endpoints;
    }

    private static async Task<IResult> ReplayAsync(
        [SkipSanitization(Reason = "The route value is parsed as a non-empty Guid before use.")]
        Guid messageId,
        OutboxOperationsService operationsService,
        CancellationToken cancellationToken)
    {
        var found = await operationsService.ReplayAsync(messageId, cancellationToken);
        return found ? Results.NoContent() : Results.NotFound();
    }
}
