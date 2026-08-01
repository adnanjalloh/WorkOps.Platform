using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace WorkOps.Api.Authentication;

internal static class AuthenticationExtensions
{
    public static IServiceCollection AddWorkOpsAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("Authentication");
        var issuer = section["Issuer"]
            ?? throw new InvalidOperationException("Authentication:Issuer must be configured.");
        var audience = section["Audience"]
            ?? throw new InvalidOperationException("Authentication:Audience must be configured.");
        var allowedAlgorithms = section.GetSection("AllowedAlgorithms").Get<string[]>()
            ?? [SecurityAlgorithms.RsaSha256];
        var metadataAddress = section["MetadataAddress"];

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Authority = issuer;
                options.Audience = audience;
                if (!string.IsNullOrWhiteSpace(metadataAddress))
                {
                    options.MetadataAddress = metadataAddress;
                }

                options.RequireHttpsMetadata = section.GetValue("RequireHttpsMetadata", true);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    ValidAlgorithms = allowedAlgorithms,
                };
            });

        return services;
    }
}
