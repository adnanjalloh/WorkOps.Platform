using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using WorkOps.Application.Common;

namespace WorkOps.Api.Observability;

internal static class ObservabilityExtensions
{
    public static ConfigureHostBuilder UseWorkOpsLogging(this ConfigureHostBuilder host)
    {
        host.UseSerilog(
            (_, services, logger) =>
            {
                logger
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("application", "WorkOps.Api")
                    .WriteTo.Console(new JsonFormatter(renderMessage: true));
                foreach (var sink in services.GetServices<ILogEventSink>())
                {
                    logger.WriteTo.Sink(sink);
                }
            });
        return host;
    }

    public static IServiceCollection AddWorkOpsObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpSection = configuration.GetSection("Observability:Otlp");
        var exportEnabled = otlpSection.GetValue("Enabled", false);
        var allowInsecureTransport = otlpSection.GetValue("AllowInsecureTransport", false);
        var endpoint = otlpSection["Endpoint"];
        var headers = otlpSection["Headers"];
        Uri? exportEndpoint = null;
        if (exportEnabled &&
            (!Uri.TryCreate(endpoint, UriKind.Absolute, out exportEndpoint) ||
             exportEndpoint.Scheme is not ("http" or "https")))
        {
            throw new InvalidOperationException(
                "Observability:Otlp:Endpoint must be an absolute HTTP or HTTPS URI.");
        }

        if (exportEndpoint?.Scheme == Uri.UriSchemeHttp && !allowInsecureTransport)
        {
            throw new InvalidOperationException(
                "HTTP OTLP export requires Observability:Otlp:AllowInsecureTransport=true.");
        }

        if (!string.IsNullOrWhiteSpace(headers) &&
            (!exportEnabled || exportEndpoint?.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "OTLP headers require enabled HTTPS export.");
        }

        var telemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: "WorkOps.Api",
                serviceVersion: BuildVersion.Current));

        telemetry.WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health/live");
                })
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddSource("WorkOps.Messaging");
            if (exportEndpoint is not null)
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = exportEndpoint;
                    options.Headers = headers;
                });
            }
        });
        telemetry.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddNpgsqlInstrumentation()
                .AddMeter("WorkOps.Cache", "WorkOps.Idempotency", "WorkOps.Messaging");
            if (exportEndpoint is not null)
            {
                metrics.AddOtlpExporter(options =>
                {
                    options.Endpoint = exportEndpoint;
                    options.Headers = headers;
                });
            }
        });

        return services;
    }
}
