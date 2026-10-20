using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillBridge.BuildingBlocks.Modules;
using SkillBridge.Modules.Learning.Application;
using SkillBridge.Modules.Learning.Endpoints;
using SkillBridge.Modules.Learning.Infrastructure;

namespace SkillBridge.Modules.Learning;

public sealed class LearningModule : ModuleDefinition
{
    public override string Name => "Learning";
    public override string RoutePrefix => "learning";
    public override void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        services.AddDbContext<LearningDbContext>(options => options.UseNpgsql(connection,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", LearningDbContext.Schema)));
        services.TryAddScoped<ILearningBookingDirectory, UnavailableLearningBookings>();
        services.AddScoped<LearningSessions>();
    }
    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        base.MapEndpoints(endpoints); endpoints.MapLearningEndpoints();
    }
    public override async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LearningDbContext>().Database.MigrateAsync();
    }
}
