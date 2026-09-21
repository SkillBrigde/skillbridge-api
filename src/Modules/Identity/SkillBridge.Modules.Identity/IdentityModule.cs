using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkillBridge.BuildingBlocks.Contracts;
using SkillBridge.Modules.Identity.Endpoints;
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
                // Đặt bảng lịch sử migration vào đúng schema 'identity' của module
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema);
            });
        });
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Kế thừa probe endpoint: GET /api/v1/identity/_module
        base.MapEndpoints(endpoints);

        // Đăng ký các endpoints thực tế của Identity module
        endpoints.MapAuthEndpoints();
        endpoints.MapUsersEndpoints();
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
                logger.LogInformation("Đang kiểm tra và áp dụng pending migrations cho Module Identity...");
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Migrations cho Module Identity đã hoàn tất.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể tự động migrate IdentityDbContext. Hãy kiểm tra PostgreSQL container đang chạy.");
        }
    }
}
