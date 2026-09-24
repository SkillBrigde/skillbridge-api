using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SkillBridge.Modules.Catalog.Infrastructure;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Api.Common;

public sealed class DatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        DbContext[] databases = [
            scope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
            scope.ServiceProvider.GetRequiredService<CatalogDbContext>()
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
