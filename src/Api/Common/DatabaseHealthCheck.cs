using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SkillBridge.Modules.Catalog.Infrastructure;
using SkillBridge.Modules.Identity.Infrastructure.Persistence;
using SkillBridge.Modules.Profiles.Infrastructure;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Api.Common;

public sealed class DatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        DbContext[] databases = [
            scope.ServiceProvider.GetRequiredService<SkillBridge.Modules.Messaging.Infrastructure.MessagingDbContext>(),
            scope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
            scope.ServiceProvider.GetRequiredService<CatalogDbContext>(),
            scope.ServiceProvider.GetRequiredService<ProfilesDbContext>(),
            scope.ServiceProvider.GetRequiredService<SchedulingDbContext>()
        ];

        foreach (var database in databases)
        {
            if (!await database.Database.CanConnectAsync(cancellationToken)
                || (await database.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                return HealthCheckResult.Unhealthy("Database unavailable or module migrations pending.");
            }
        }

        return HealthCheckResult.Healthy();
    }
}
