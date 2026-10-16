
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillBridge.BuildingBlocks.Contracts;
using SkillBridge.Modules.Catalog.Application;
using SkillBridge.Modules.Catalog.Endpoints;
using SkillBridge.Modules.Catalog.Infrastructure;

namespace SkillBridge.Modules.Catalog;

public sealed class CatalogModule : ModuleDefinition
{
    public override string Name => "Catalog";
    public override string RoutePrefix => "catalog";

    public override void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString,
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", CatalogDbContext.Schema)));
        services.AddScoped<CatalogService>();
        services.AddScoped<MentorServices>();
        services.TryAddScoped<IMentorDirectory, UnavailableMentorDirectory>();
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        base.MapEndpoints(endpoints);
        endpoints.MapCatalogEndpoints();
        endpoints.MapMentorServiceEndpoints();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
    }
}
