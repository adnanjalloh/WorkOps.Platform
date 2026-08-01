using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using WorkOps.Api.Authentication;
using WorkOps.Api.Authorization;
using WorkOps.Api.Endpoints;
using WorkOps.Api.Errors;
using WorkOps.Api.Tenancy;
using WorkOps.Application;
using WorkOps.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddWorkOpsApplication();
builder.Services.AddWorkOpsInfrastructure(builder.Configuration);
builder.Services.AddWorkOpsAuthentication(builder.Configuration);
builder.Services.AddWorkOpsAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseRouting();
app.UseAuthentication();
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
