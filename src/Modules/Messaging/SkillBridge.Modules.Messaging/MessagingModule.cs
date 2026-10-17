using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
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
        services.AddScoped<IMessageNotifier, SignalRMessageNotifier>();
        services.AddSingleton<ChatConnections>();
        services.AddSignalR(options => options.MaximumReceiveMessageSize = 32 * 1024);
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var previous = options.Events.OnMessageReceived;
            options.Events.OnMessageReceived = async context =>
            {
                await previous(context);
                if (string.IsNullOrEmpty(context.Token) && !context.Request.Headers.ContainsKey("Authorization") &&
                    context.Request.Path.StartsWithSegments("/hubs/chat") &&
                    context.Request.Query.TryGetValue("access_token", out var tokens) && tokens.Count == 1)
                    context.Token = tokens[0];
            };
        });
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        base.MapEndpoints(endpoints);
        endpoints.MapMessagingEndpoints();
        endpoints.MapHub<ChatHub>("/hubs/chat", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Database.MigrateAsync();
    }
}
