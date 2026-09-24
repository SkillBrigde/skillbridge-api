using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SkillBridge.BuildingBlocks.Security;

public static class SecurityExtensions
{
    public static IServiceCollection AddJwtSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = new JwtSettings();
        configuration.GetSection(JwtSettings.SectionName).Bind(jwtSettings);
        if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey)
            || Encoding.UTF8.GetByteCount(jwtSettings.SecretKey) < 32
            || string.IsNullOrWhiteSpace(jwtSettings.Issuer)
            || string.IsNullOrWhiteSpace(jwtSettings.Audience)
            || jwtSettings.AccessTokenExpirationMinutes is <= 0 or > 60
            || jwtSettings.RefreshTokenExpirationDays is <= 0 or > 90)
        {
            throw new InvalidOperationException("Configure Jwt with a secret of at least 32 bytes, issuer, audience, access lifetime 1–60 minutes and refresh lifetime 1–90 days.");
        }
        services.AddSingleton(jwtSettings);

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

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
