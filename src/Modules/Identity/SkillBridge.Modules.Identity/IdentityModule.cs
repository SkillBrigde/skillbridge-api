using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkillBridge.BuildingBlocks.Contracts;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity;

public sealed class IdentityModule : ModuleDefinition
{
    public override string Name => "Identity";
    public override string RoutePrefix => "identity";

    public override void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? "Host=localhost;Port=5432;Database=skillbridge;Username=skillbridge;Password=skillbridge_dev_only";

        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // Äáº·t báº£ng lá»‹ch sá»­ migration vĂ o Ä‘Ăºng schema 'identity' cá»§a module
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema);
            });
        });
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Káº¿ thá»«a probe endpoint: GET /api/v1/identity/_module
        base.MapEndpoints(endpoints);

        var group = endpoints.MapGroup($"/api/v1/{RoutePrefix}").WithTags(Name);

        // Endpoint test nghiá»‡p vá»¥ máº«u
        group.MapGet("/users/me", () =>
        {
            var sampleUser = User.Create("mentor@skillbridge.dev", "Nguyen Van A", "Mentor");

            return Microsoft.AspNetCore.Http.Results.Ok(new
            {
                sampleUser.Id,
                sampleUser.Email,
                sampleUser.FullName,
                sampleUser.Role,
                sampleUser.CreatedAtUtc
            });
        });

        // Endpoint láº¥y danh sĂ¡ch users tá»« database
        group.MapGet("/users", async (IdentityDbContext dbContext) =>
        {
            var users = await dbContext.Users
                .AsNoTracking()
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FullName,
                    u.Role,
                    u.IsActive,
                    u.CreatedAtUtc
                })
                .ToListAsync();

            return Microsoft.AspNetCore.Http.Results.Ok(users);
        });
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("IdentityModule");

        try
        {
            var dbContext = scope.ServiceProvider.GetService<IdentityDbContext>();
            if (dbContext is not null)
            {
                logger.LogInformation("Äang kiá»ƒm tra vĂ  Ă¡p dá»¥ng pending migrations cho Module Identity...");
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Migrations cho Module Identity Ä‘Ă£ hoĂ n táº¥t.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "KhĂ´ng thá»ƒ tá»± Ä‘á»™ng migrate IdentityDbContext. HĂ£y kiá»ƒm tra PostgreSQL container Ä‘ang cháº¡y.");
        }
    }
}
