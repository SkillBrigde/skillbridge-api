using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkillBridge.Modules.Payments.Application;

namespace SkillBridge.Modules.Payments.Infrastructure;

internal sealed class PaymentReconciliationWorker(IServiceScopeFactory scopes, ILogger<PaymentReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var ids = await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Orders.AsNoTracking()
                    .Where(x => x.Status == "Verified").OrderBy(x => x.VerifiedAtUtc).Take(100).Select(x => x.Id).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                        await using var item = scopes.CreateAsyncScope();
                        var result = await item.ServiceProvider.GetRequiredService<PaymentsService>().ApplyAsync(id, stoppingToken);
                        if (result.IsFailure) logger.LogWarning("Payment {PaymentId} awaits Booking: {Code}", id, result.Error.Code);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception) { logger.LogError(exception, "Payment {PaymentId} reconciliation failed.", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Payment reconciliation worker failed."); }
        }
    }
}
