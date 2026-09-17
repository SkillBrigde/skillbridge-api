using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SkillBridge.BuildingBlocks.Contracts;
using SkillBridge.Modules.Identity.Application;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Endpoints;
using SkillBridge.Modules.Identity.Infrastructure.Data;
using SkillBridge.Modules.Identity.Infrastructure.Security;

namespace SkillBridge.Modules.Identity;

public sealed class IdentityModule : ModuleDefinition
{
    public override string Name => "Identity";
    public override string RoutePrefix => "identity";

    public override void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("Database"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema)));
        services.AddScoped<IIdentityData>(provider => provider.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IdentityService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<SessionValidationEvents>();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection("Jwt"))
            .Validate(x => Encoding.UTF8.GetByteCount(x.SigningKey) >= 32, "Jwt:SigningKey requires at least 32 UTF-8 bytes.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience),
                "Jwt issuer and audience are required.").ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwt) =>
            {
                options.MapInboundClaims = false;
                options.EventsType = typeof(SessionValidationEvents);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });
        services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole("Admin", "SuperAdmin"));
        services.AddHealthChecks().AddCheck<IdentityDatabaseHealthCheck>("identity-database", tags: ["ready"]);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        base.MapEndpoints(endpoints);
        endpoints.MapIdentityEndpoints();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    }
}

internal sealed class IdentityDatabaseHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        try
        {
            // Query a migrated column as well as connectivity, so an uninitialized DB is not ready.
            await db.Users.Select(x => x.SecurityStamp).Take(1).ToListAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Identity database is unavailable or requires migrations.", ex);
        }
    }
}
