using WorkOps.Application.Abstractions;
using WorkOps.Application.Common.Pagination;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Common.Validation;

namespace WorkOps.Application.Audit;

public sealed class AuditService(IAuditStore auditStore, IInputSanitizer sanitizer)
{
    public Task<PagedResult<AuditEventView>> ListAsync(
        int page,
        int pageSize,
        string? action,
        string? entityType,
        CancellationToken cancellationToken)
    {
        if (page is < 1 or > 10_000 || pageSize is < 1 or > 100)
        {
            throw new RequestValidationException("invalid_pagination");
        }

        var safeAction = SanitizeOptionalIdentifier(action, "query.action");
        var safeEntityType = SanitizeOptionalIdentifier(entityType, "query.entityType");
        return auditStore.ListAsync(
            page,
            pageSize,
            safeAction,
            safeEntityType,
            cancellationToken);
    }

    private string? SanitizeOptionalIdentifier(string? value, string path) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : sanitizer.Apply(value, InputProfile.Identifier, path);
}
