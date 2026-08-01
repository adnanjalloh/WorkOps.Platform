using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkOps.Application.Abstractions;
using WorkOps.Infrastructure.Health;
using WorkOps.Infrastructure.Identity;
using WorkOps.Infrastructure.Persistence;
using WorkOps.Infrastructure.Tenancy;

namespace WorkOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkOpsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WorkOps");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:WorkOps must be configured.");
        }

        services.AddDbContext<WorkOpsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<WorkOpsDbContext>());
        services.AddScoped<IUserStore, UserStore>();
        services.AddScoped<IWorkspaceStore, WorkspaceStore>();
        services.AddScoped<IWorkspaceAccessReader, WorkspaceAccessReader>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>(
            "postgresql",
            tags: ["ready"]);
        return services;
    }

    public static async Task ApplyWorkOpsMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkOpsDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
