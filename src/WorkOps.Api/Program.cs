using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using WorkOps.Api.Authentication;
using WorkOps.Api.Authorization;
using WorkOps.Api.Endpoints;
using WorkOps.Api.Errors;
using WorkOps.Api.Observability;
using WorkOps.Api.Operations;
using WorkOps.Api.Tenancy;
using WorkOps.Application;
using WorkOps.Application.Abstractions;
using WorkOps.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWorkOpsLogging();
ProductionConfiguration.Validate(builder.Configuration, builder.Environment);
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 1_048_576;
    options.Limits.MaxRequestHeadersTotalSize = 32_768;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Extensions["traceId"] =
        Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddWorkOpsApplication();
builder.Services.AddWorkOpsInfrastructure(builder.Configuration);
builder.Services.AddWorkOpsAuthentication(builder.Configuration);
builder.Services.AddWorkOpsAuthorization();
builder.Services.AddWorkOpsHttpHardening(builder.Configuration);
builder.Services.AddWorkOpsObservability(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} returned {StatusCode} in {Elapsed:0.0000} ms";
});
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors(HttpHardeningExtensions.CorsPolicy);
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<WorkspaceContextMiddleware>();
app.UseAuthorization();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions { Predicate = static _ => false });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions { Predicate = static check => check.Tags.Contains("ready") });
app.MapIdentityEndpoints();
app.MapWorkspaceEndpoints();
app.MapProjectEndpoints();
app.MapWorkItemEndpoints();
app.MapAuditEndpoints();
app.MapNotificationEndpoints();
app.MapOperationsEndpoints();
app.MapFeatureEndpoints();
app.MapAttachmentEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (builder.Configuration.GetValue("Operations:ApplyMigrations", false))
{
    if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    {
        throw new InvalidOperationException("Automatic migrations are restricted to local and test environments.");
    }

    await app.Services.ApplyWorkOpsMigrationsAsync();
}

app.Run();

public partial class Program;
