using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace WorkOps.Api.Operations;

internal static class HttpHardeningExtensions
{
    public const string CorsPolicy = "workops-api";

    public static IServiceCollection AddWorkOpsHttpHardening(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(static origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https") ||
                origin.EndsWith('/')))
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must contain absolute HTTP or HTTPS origins without trailing slashes.");
        }

        services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
            }
        }));

        var permitLimit = configuration.GetValue("RateLimiting:PermitLimit", 60);
        var windowSeconds = configuration.GetValue("RateLimiting:WindowSeconds", 60);
        if (permitLimit is < 1 or > 10_000 || windowSeconds is < 1 or > 3_600)
        {
            throw new InvalidOperationException("RateLimiting configuration is outside safe bounds.");
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Path.StartsWithSegments("/health"))
                {
                    return RateLimitPartition.GetNoLimiter("health");
                }

                var subject = context.User.FindFirstValue("sub");
                var partition = subject is null
                    ? $"ip:{context.Connection.RemoteIpAddress}"
                    : $"subject:{subject}";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partition,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = permitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                    });
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = windowSeconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                await Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Request rate limit exceeded",
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "rate_limit_exceeded",
                        })
                    .ExecuteAsync(context.HttpContext);
                _ = cancellationToken;
            };
        });

        return services;
    }
}
