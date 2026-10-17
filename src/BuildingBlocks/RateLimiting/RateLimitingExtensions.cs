using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace SkillBridge.BuildingBlocks.RateLimiting;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddRedisRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure Redis__ConnectionString before starting the API.");
        }

        var options = ConfigurationOptions.Parse(connectionString);

        options.AbortOnConnectFail = false;
        options.ConnectRetry = 1;
        options.ConnectTimeout = 2000;
        options.SyncTimeout = 2000;
        options.AsyncTimeout = 2000;
        options.BacklogPolicy = BacklogPolicy.FailFast;

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(options));

        services.AddSingleton<IRateLimitStore, RedisRateLimitStore>();

        return services;
    }
}