using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions { Predicate = static _ => false });
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program;
