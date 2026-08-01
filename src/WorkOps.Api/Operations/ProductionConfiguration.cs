using Microsoft.Extensions.Hosting;

namespace WorkOps.Api.Operations;

internal static class ProductionConfiguration
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            return;
        }

        if (!configuration.GetValue("Authentication:RequireHttpsMetadata", true))
        {
            throw new InvalidOperationException(
                "Authentication metadata must require HTTPS outside development and testing.");
        }

        if (configuration.GetValue("Files:DevelopmentScannerEnabled", false))
        {
            throw new InvalidOperationException(
                "The development file scanner cannot be enabled outside development and testing.");
        }

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(static origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Production CORS origins must use HTTPS.");
        }
    }
}
