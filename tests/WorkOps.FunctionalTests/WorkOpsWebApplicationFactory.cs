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

    public RecordingMessagePublisher Publisher { get; } = new();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        _ = Server;
        await Services.ApplyWorkOpsMigrationsAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Audience"] = Audience,
                ["Authentication:Issuer"] = Issuer,
                ["Authentication:RequireHttpsMetadata"] = "false",
                ["Authentication:AllowedAlgorithms:0"] = SecurityAlgorithms.HmacSha256,
                ["ConnectionStrings:WorkOps"] = _database.GetConnectionString(),
                ["Operations:ApplyMigrations"] = "false",
            });
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
    }
}
