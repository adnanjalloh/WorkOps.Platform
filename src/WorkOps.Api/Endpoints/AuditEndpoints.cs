using System.Text.Json;
using WorkOps.Api.Tenancy;
using WorkOps.Application.Audit;
using WorkOps.Contracts.Audit;
using WorkOps.Contracts.Common;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Endpoints;

internal static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/audit-events", ListAsync)
            .RequireAuthorization(Permissions.AuditRead)
            .WithMetadata(new WorkspaceContextRequirement(WorkspaceContextSource.Header))
            .WithName("ListAuditEvents");
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        AuditService auditService,
        CancellationToken cancellationToken,
        [SkipSanitization(Reason = "The query value is parsed as an integer and range validated before use.")]
        int page = 1,
        [SkipSanitization(Reason = "The query value is parsed as an integer and range validated before use.")]
        int pageSize = 20,
        [SanitizeAs(SanitizationProfile.Identifier)] string? action = null,
        [SanitizeAs(SanitizationProfile.Identifier)] string? entityType = null)
    {
        var result = await auditService.ListAsync(
            page,
            pageSize,
            action,
            entityType,
            cancellationToken);
        var items = result.Items.Select(auditEvent => new AuditEventResponse(
            auditEvent.Id,
            auditEvent.ActorUserId,
            auditEvent.Action,
            auditEvent.EntityType,
            auditEvent.EntityId,
            auditEvent.OccurredAt,
            auditEvent.CorrelationId,
            DeserializeMetadata(auditEvent.MetadataJson)))
            .ToArray();
        return Results.Ok(new PagedResponse<AuditEventResponse>(
            items,
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    private static Dictionary<string, string> DeserializeMetadata(string metadataJson) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson)
        ?? new Dictionary<string, string>(StringComparer.Ordinal);
}
