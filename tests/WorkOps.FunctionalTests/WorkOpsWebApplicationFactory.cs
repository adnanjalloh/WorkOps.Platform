using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog.Core;
using Serilog.Events;
using Testcontainers.PostgreSql;
using WorkOps.Application.Abstractions;
using WorkOps.Infrastructure;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.FunctionalTests;

internal sealed class WorkOpsWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string Audience = "workops-functional-api";
    public const string Issuer = "https://identity.test.invalid/realms/workops";

    public static readonly SymmetricSecurityKey SigningKey = new(
        Enumerable.Range(1, 64).Select(static value => (byte)value).ToArray());

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18.4-alpine")
        .Build();
    private readonly string _fileRoot = Path.Combine(
        Path.GetTempPath(),
        $"workops-functional-{Guid.NewGuid():N}");
    private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;

    public RecordingMessagePublisher Publisher { get; } = new();

    public RecordingLogSink Logs { get; } = new();

    public WorkOpsWebApplicationFactory(
        string environment = "Testing",
        IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        EnvironmentName = environment;
        _configurationOverrides = configurationOverrides ??
                                  new Dictionary<string, string?>(StringComparer.Ordinal);
    }

    public string EnvironmentName { get; }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        _ = Server;
        await Services.ApplyWorkOpsMigrationsAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedHosts"] = "localhost;workops.test",
                ["Authentication:Audience"] = Audience,
                ["Authentication:Issuer"] = Issuer,
                ["Authentication:RequireHttpsMetadata"] =
                    (EnvironmentName == "Production").ToString(),
                ["Authentication:AllowedAlgorithms:0"] = SecurityAlgorithms.HmacSha256,
                ["ConnectionStrings:WorkOps"] = _database.GetConnectionString(),
                ["Operations:ApplyMigrations"] = "false",
                ["Cache:Enabled"] = "false",
                ["Files:DevelopmentScannerEnabled"] =
                    (EnvironmentName != "Production").ToString(),
                ["Files:RootPath"] = _fileRoot,
                ["Idempotency:PurgeEnabled"] = "false",
            });
            configuration.AddInMemoryCollection(_configurationOverrides);
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<WorkOpsDbContext>();
            services.RemoveAll<DbContextOptions<WorkOpsDbContext>>();
            services.AddDbContext<WorkOpsDbContext>(
                options => options.UseNpgsql(_database.GetConnectionString()));
            services.RemoveAll<IMessagePublisher>();
            services.AddSingleton(Publisher);
            services.AddSingleton<IMessagePublisher>(Publisher);
            services.AddSingleton<ILogEventSink>(Logs);
            services.RemoveAll<IFileScanner>();
            services.AddSingleton<IFileScanner, FunctionalFileScanner>();
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(new FunctionalFileStorage(_fileRoot));

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var identityConfiguration = new OpenIdConnectConfiguration
                {
                    Issuer = Issuer,
                };
                identityConfiguration.SigningKeys.Add(SigningKey);

                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(identityConfiguration);
                options.TokenValidationParameters.ValidIssuer = Issuer;
                options.TokenValidationParameters.ValidAudience = Audience;
                options.TokenValidationParameters.IssuerSigningKey = SigningKey;
                options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
            });
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
        if (Directory.Exists(_fileRoot))
        {
            Directory.Delete(_fileRoot, recursive: true);
        }
    }
}

internal sealed class RecordingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }

    public void Emit(LogEvent logEvent)
    {
        _messages.Enqueue(
            $"{logEvent.RenderMessage(System.Globalization.CultureInfo.InvariantCulture)}|{logEvent.Exception}");
    }
}

file sealed class FunctionalFileScanner : IFileScanner
{
    public Task<FileScanResult> ScanAsync(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        _ = content;
        _ = cancellationToken;
        return Task.FromResult(FileScanResult.Clean);
    }
}

file sealed class FunctionalFileStorage(string rootPath) : IFileStorage
{
    public async Task SaveAsync(
        WorkOps.Domain.WorkspaceId workspaceId,
        string storageName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var path = Resolve(workspaceId, storageName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content.ToArray(), cancellationToken);
    }

    public Task<Stream> OpenReadAsync(
        WorkOps.Domain.WorkspaceId workspaceId,
        string storageName,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        Stream content = File.OpenRead(Resolve(workspaceId, storageName));
        return Task.FromResult(content);
    }

    public Task DeleteAsync(
        WorkOps.Domain.WorkspaceId workspaceId,
        string storageName,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        File.Delete(Resolve(workspaceId, storageName));
        return Task.CompletedTask;
    }

    private string Resolve(WorkOps.Domain.WorkspaceId workspaceId, string storageName) =>
        Path.Combine(rootPath, workspaceId.Value.ToString("N"), storageName);
}
