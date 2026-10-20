using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkillBridge.Modules.Recommendations.Application;

namespace SkillBridge.Modules.Recommendations.Infrastructure;

internal sealed class MetricsWorker(IServiceScopeFactory scopes, ILogger<MetricsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<RecommendationsService>().RefreshMetricsAsync(stoppingToken);
                if (result.IsFailure) logger.LogWarning("Metrics refresh deferred: {Code}", result.Error.Code);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Recommendation metrics refresh failed; previous snapshot remains available."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
