using System.Threading.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SkillBridge.Api.Common;
using SkillBridge.Api.Middleware;
using SkillBridge.Api.Security;
using SkillBridge.BuildingBlocks;
using SkillBridge.BuildingBlocks.Modules;
using SkillBridge.BuildingBlocks.Security;

namespace SkillBridge.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    internal const string CorsPolicyName = "Default";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyCollection<IModule> modules)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance = context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddOpenApi();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                $"{context.Connection.RemoteIpAddress}:{context.GetEndpoint()?.DisplayName}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
                else
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            });
        });

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        var moduleAssemblies = modules.Select(module => module.GetType().Assembly).Distinct().ToArray();
        services.AddBuildingBlocks(configuration, moduleAssemblies);
        services.AddSecurityServices(configuration);
        services.AddApiAuthentication();

        foreach (var module in modules)
        {
            module.AddServices(services, configuration);
        }

        services.AddMessagingComposition();
        services.AddCatalogComposition();
        services.AddProfilesComposition();
        services.AddSchedulingComposition();
        services.AddBookingComposition();
        services.AddLearningComposition();
        services.AddPaymentsComposition();

        return services;
    }
}
