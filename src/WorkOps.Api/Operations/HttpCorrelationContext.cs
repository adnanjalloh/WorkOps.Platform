using WorkOps.Application.Abstractions;

namespace WorkOps.Api.Operations;

internal sealed class HttpCorrelationContext(IHttpContextAccessor httpContextAccessor)
    : ICorrelationContext
{
    public string CorrelationId => httpContextAccessor.HttpContext?.TraceIdentifier
        ?? throw new InvalidOperationException("An HTTP request context is required.");
}
