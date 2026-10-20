using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillBridge.BuildingBlocks.Modules;
using SkillBridge.Modules.Reviews.Application;
using SkillBridge.Modules.Reviews.Endpoints;
using SkillBridge.Modules.Reviews.Infrastructure;

namespace SkillBridge.Modules.Reviews;

public sealed class ReviewsModule : ModuleDefinition
{
    public override string Name => "Reviews";
    public override string RoutePrefix => "reviews";
    public override void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        services.AddDbContext<ReviewsDbContext>(options => options.UseNpgsql(connection,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ReviewsDbContext.Schema)));
        services.TryAddScoped<IReviewEvidence, UnavailableReviewEvidence>(); services.AddScoped<ReviewsService>();
    }
    public override void MapEndpoints(IEndpointRouteBuilder endpoints) { base.MapEndpoints(endpoints); endpoints.MapReviewEndpoints(); }
    public override async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ReviewsDbContext>().Database.MigrateAsync();
    }
}
