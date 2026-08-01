using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using WorkOps.Application.Abstractions;
using WorkOps.Infrastructure.Audit;
using WorkOps.Infrastructure.Features;
using WorkOps.Infrastructure.Files;
using WorkOps.Infrastructure.Health;
using WorkOps.Infrastructure.Identity;
using WorkOps.Infrastructure.Messaging;
using WorkOps.Infrastructure.Notifications;
using WorkOps.Infrastructure.Persistence;
using WorkOps.Infrastructure.Projects;
using WorkOps.Infrastructure.Tenancy;
using WorkOps.Infrastructure.WorkItems;

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
        services.AddScoped<IProjectStore, ProjectStore>();
        services.AddScoped<IWorkItemStore, WorkItemStore>();
        services.AddScoped<IAuditStore, AuditStore>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<INotificationStore, NotificationStore>();
        services.AddScoped<IWorkspaceSubscriptionStore, WorkspaceSubscriptionStore>();
        services.AddScoped<IAttachmentStore, AttachmentStore>();
        var fileRoot = configuration["Files:RootPath"]
            ?? Path.Combine(Path.GetTempPath(), "workops-attachments");
        services.AddSingleton<IFileStorage>(new LocalFileStorage(fileRoot));
        if (bool.TryParse(configuration["Files:DevelopmentScannerEnabled"], out var scannerEnabled) &&
            scannerEnabled)
        {
            services.AddSingleton<IFileScanner, DevelopmentFileScanner>();
        }
        else
        {
            services.AddSingleton<IFileScanner, RejectingFileScanner>();
        }
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>(
            "postgresql",
            tags: ["ready"]);

        if (bool.TryParse(configuration["Messaging:Enabled"], out var messagingEnabled) &&
            messagingEnabled)
        {
            var settings = CreateRabbitMqSettings(configuration);
            services.AddSingleton(settings);
            services.RemoveAll<IMessagePublisher>();
            services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
            services.AddHostedService<OutboxWorker>();
            services.AddHostedService<RabbitMqNotificationConsumer>();
            services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>(
                "rabbitmq",
                tags: ["ready"]);
        }

        if (bool.TryParse(configuration["Cache:Enabled"], out var cacheEnabled) && cacheEnabled)
        {
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (string.IsNullOrWhiteSpace(redisConnectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:Redis must be configured.");
            }

            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisOptions));
            services.RemoveAll<IFeatureCache>();
            services.AddSingleton<IFeatureCache, RedisFeatureCache>();
            services.AddHealthChecks().AddCheck<RedisHealthCheck>(
                "redis",
                tags: ["ready"]);
        }

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

    private static RabbitMqSettings CreateRabbitMqSettings(IConfiguration configuration)
    {
        var hostName = configuration["Messaging:HostName"];
        var virtualHost = configuration["Messaging:VirtualHost"] ?? "/";
        var userName = configuration["Messaging:UserName"];
        var password = configuration["Messaging:Password"];
        var port = int.TryParse(configuration["Messaging:Port"], out var configuredPort)
            ? configuredPort
            : 5672;
        if (string.IsNullOrWhiteSpace(hostName) ||
            string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(password) ||
            port is < 1 or > 65_535)
        {
            throw new InvalidOperationException("Messaging configuration is incomplete.");
        }

        return new RabbitMqSettings(hostName, port, virtualHost, userName, password);
    }
}
