using System.Diagnostics;

namespace WorkOps.Api.Operations;

internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(
        HttpContext httpContext,
        ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        httpContext.TraceIdentifier = correlationId;
        httpContext.Response.OnStarting(() =>
        {
            httpContext.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["correlationId"] = correlationId,
            ["traceId"] = Activity.Current?.TraceId.ToString() ?? correlationId,
        }))
        {
            await next(httpContext);
        }
    }
}
