using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

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
        var endpoint = otlpSection["Endpoint"];
        Uri? exportEndpoint = null;
        if (exportEnabled &&
            (!Uri.TryCreate(endpoint, UriKind.Absolute, out exportEndpoint) ||
             exportEndpoint.Scheme is not ("http" or "https")))
        {
            throw new InvalidOperationException(
                "Observability:Otlp:Endpoint must be an absolute HTTP or HTTPS URI.");
        }

        var telemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: "WorkOps.Api",
                serviceVersion: "0.1.0"));

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
                tracing.AddOtlpExporter(options => options.Endpoint = exportEndpoint);
            }
        });
        telemetry.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddNpgsqlInstrumentation()
                .AddMeter("WorkOps.Cache", "WorkOps.Messaging");
            if (exportEndpoint is not null)
            {
                metrics.AddOtlpExporter(options => options.Endpoint = exportEndpoint);
            }
        });

        return services;
    }
}
