using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillBridge.BuildingBlocks.Modules;
using SkillBridge.Modules.Recommendations.Application;
using SkillBridge.Modules.Recommendations.Endpoints;
using SkillBridge.Modules.Recommendations.Infrastructure;

namespace SkillBridge.Modules.Recommendations;

public sealed class RecommendationsModule : ModuleDefinition
{
    public override string Name => "Recommendations";
    public override string RoutePrefix => "recommendations";
    public override void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        services.AddDbContext<RecommendationsDbContext>(options => options.UseNpgsql(connection,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", RecommendationsDbContext.Schema)));
        services.TryAddScoped<IRecommendationSources, UnavailableRecommendationSources>(); services.AddScoped<RecommendationsService>();
        services.AddHostedService<MetricsWorker>();
    }
    public override void MapEndpoints(IEndpointRouteBuilder endpoints) { base.MapEndpoints(endpoints); endpoints.MapRecommendationEndpoints(); }
    public override async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RecommendationsDbContext>().Database.MigrateAsync();
    }
}
