using System.Net;
using Microsoft.Extensions.Hosting;

namespace WorkOps.Api.Operations;

public static class ProductionConfiguration
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

        if (configuration.GetValue("ForwardedHeaders:Enabled", false))
        {
            var knownProxies = configuration
                .GetSection("ForwardedHeaders:KnownProxies")
                .Get<string[]>() ?? [];
            var knownNetworks = configuration
                .GetSection("ForwardedHeaders:KnownNetworks")
                .Get<string[]>() ?? [];
            if (knownProxies.Count(static value => !string.IsNullOrWhiteSpace(value)) +
                knownNetworks.Count(static value => !string.IsNullOrWhiteSpace(value)) == 0)
            {
                throw new InvalidOperationException(
                    "Production forwarded headers require an explicit trusted proxy or network.");
            }
        }

        ValidateOtlp(configuration);

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(static origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Production CORS origins must use HTTPS.");
        }
    }

    private static void ValidateOtlp(IConfiguration configuration)
    {
        var section = configuration.GetSection("Observability:Otlp");
        if (!section.GetValue("Enabled", false))
        {
            return;
        }

        if (!Uri.TryCreate(section["Endpoint"], UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttp &&
            endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Production OTLP export requires an absolute HTTP or HTTPS endpoint.");
        }

        var allowInsecure = section.GetValue("AllowInsecureTransport", false);
        if (endpoint.Scheme == Uri.UriSchemeHttp &&
            (!allowInsecure || !IsTrustedInternalCollector(endpoint.Host)))
        {
            throw new InvalidOperationException(
                "Production HTTP OTLP export requires explicit opt-in and a trusted internal collector.");
        }

        if (!string.IsNullOrWhiteSpace(section["Headers"]) &&
            endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Production OTLP headers require HTTPS transport.");
        }
    }

    private static bool IsTrustedInternalCollector(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            !host.Contains('.', StringComparison.Ordinal) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal)
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? bytes[0] == 10 ||
              bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
              bytes[0] == 192 && bytes[1] == 168
            : bytes[0] is 0xFC or 0xFD;
    }
}
