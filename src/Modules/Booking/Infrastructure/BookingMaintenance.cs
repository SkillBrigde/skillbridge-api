using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkillBridge.Modules.Booking.Application;

namespace SkillBridge.Modules.Booking.Infrastructure;

internal sealed class BookingMaintenance(IServiceScopeFactory scopes, ILogger<BookingMaintenance> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
                var ids = await db.Bookings.AsNoTracking().Where(x => x.Status == "Holding" || x.NeedsSlotRelease ||
                    x.Status == "PendingPayment" && x.HeldUntilUtc <= now).OrderBy(x => x.UpdatedAtUtc).Select(x => x.Id).Take(100).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                        await using var itemScope = scopes.CreateAsyncScope();
                        var bookings = itemScope.ServiceProvider.GetRequiredService<Bookings>();
                        await bookings.ResumeHoldAsync(id, stoppingToken);
                        await bookings.ReconcileAsync(id, stoppingToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    { logger.LogError(exception, "Booking {BookingId} reconciliation failed; continuing the batch.", id); }
                }
                var requests = await db.RescheduleRequests.AsNoTracking().Where(x => x.Status == "Applying")
                    .OrderBy(x => x.UpdatedAtUtc).Select(x => x.Id).Take(100).ToListAsync(stoppingToken);
                foreach (var id in requests)
                {
                    try
                    {
                        await using var itemScope = scopes.CreateAsyncScope();
                        await itemScope.ServiceProvider.GetRequiredService<Reschedules>().ApplyAsync(id, stoppingToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    { logger.LogError(exception, "Reschedule {RequestId} reconciliation failed; continuing the batch.", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Booking reconciliation failed; persisted intents will be retried."); }
        }
    }
}
