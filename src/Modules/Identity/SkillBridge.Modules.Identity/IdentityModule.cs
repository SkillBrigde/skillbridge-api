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
                // Đặt bảng lịch sử migration vào đúng schema 'identity' của module
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema);
            });
        });
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Kế thừa probe endpoint: GET /api/v1/identity/_module
        base.MapEndpoints(endpoints);

        var group = endpoints.MapGroup($"/api/v1/{RoutePrefix}").WithTags(Name);

        // Endpoint test nghiệp vụ mẫu
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

        // Endpoint lấy danh sách users từ database
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
