using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SkillBridge.BuildingBlocks.Security;

namespace SkillBridge.Api.Security;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // Resolve JwtSettings that was already registered by AddSecurityServices.
        using var provider = services.BuildServiceProvider();
        var jwtSettings = provider.GetRequiredService<JwtSettings>();

        if (string.IsNullOrWhiteSpace(jwtSettings.Issuer)
            || string.IsNullOrWhiteSpace(jwtSettings.Audience)
            || jwtSettings.AccessTokenExpirationMinutes is <= 0 or > 60
            || jwtSettings.RefreshTokenExpirationDays is <= 0 or > 90)
        {
            throw new InvalidOperationException("Configure Jwt with issuer, audience, access lifetime 1–60 minutes and refresh lifetime 1–90 days.");
        }

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = "name",
                RoleClaimType = "role",
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        return services;
    }
}
