using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.BuildingBlocks.Contracts;
using SkillBridge.Modules.Messaging.Application;
using SkillBridge.Modules.Messaging.Endpoints;
using SkillBridge.Modules.Messaging.Infrastructure;

namespace SkillBridge.Modules.Messaging;

public sealed class MessagingModule : ModuleDefinition
{
    public override string Name => "Messaging";
    public override string RoutePrefix => "messaging";

    public override void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        services.AddDbContext<MessagingDbContext>(options => options.UseNpgsql(connectionString,
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", MessagingDbContext.Schema)));
        services.AddScoped<MessagingService>();
        services.AddScoped<IMessageNotifier, NoRealtimeNotifier>();
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        base.MapEndpoints(endpoints);
        endpoints.MapMessagingEndpoints();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Database.MigrateAsync();
    }
}
