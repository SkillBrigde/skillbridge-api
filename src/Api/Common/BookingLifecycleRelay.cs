using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Infrastructure;

namespace SkillBridge.Api.Common;

public interface IBookingLifecycleConsumer
{
    Task<Result> ConsumeAsync(Guid eventId, Guid bookingId, CancellationToken ct);
}

public sealed class BookingLifecycleRelay(BookingDbContext db, IEnumerable<IBookingLifecycleConsumer> consumers, TimeProvider clock,
    ILogger<BookingLifecycleRelay> logger)
{
    public async Task<bool> DispatchOneAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow();
        var messages = await db.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM booking.outbox_messages
            WHERE processed_on_utc IS NULL AND (retry_after_utc IS NULL OR retry_after_utc <= {now})
            ORDER BY occurred_on_utc, id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        if (messages.Count == 0) return false;
        var message = messages[0];
        try
        {
            var error = await DeliverAsync(message.Id, message.Type, message.Content, ct);
            if (error != Error.None)
            {
                message.Error = error.Code;
                db.Entry(message).Property<DateTimeOffset?>("RetryAfterUtc").CurrentValue = clock.GetUtcNow().AddSeconds(30);
                logger.LogWarning("Booking outbox {EventId} deferred: {ErrorCode}.", message.Id, error.Code);
            }
            else
            {
                message.ProcessedOnUtc = clock.GetUtcNow(); message.Error = null;
                db.Entry(message).Property<DateTimeOffset?>("RetryAfterUtc").CurrentValue = null;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Booking outbox {EventId} failed; retrying after 30 seconds.", message.Id);
            message.Error = "Delivery failed; inspect Host logs.";
            db.Entry(message).Property<DateTimeOffset?>("RetryAfterUtc").CurrentValue = clock.GetUtcNow().AddSeconds(30);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return true;
    }

    private async Task<Error> DeliverAsync(Guid eventId, string type, string content, CancellationToken ct)
    {
        if (type is not ("Booking.Confirmed.v1" or "Booking.Cancelled.v1" or "Booking.RefundReview.v1" or "Booking.Completed.v1" or "Booking.Rescheduled.v1"))
            return Error.Failure("Host.UnknownBookingEvent", "Loại sự kiện Booking chưa được hỗ trợ.");
        using var json = JsonDocument.Parse(content);
        if (!json.RootElement.TryGetProperty("Id", out var id) || !id.TryGetGuid(out var bookingId) || bookingId == Guid.Empty)
            return Error.Failure("Host.InvalidBookingEvent", "Sự kiện thiếu định danh booking.");
        var sinks = consumers.ToArray();
        if (sinks.Length == 0) return Error.Failure("Host.NoBookingConsumer", "Chưa cấu hình consumer.");
        foreach (var consumer in sinks)
        {
            var result = await consumer.ConsumeAsync(eventId, bookingId, ct);
            if (result.IsFailure) return result.Error;
        }
        return Error.None;
    }
}

internal sealed class BookingLifecycleWorker(IServiceScopeFactory scopes, ILogger<BookingLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                for (var index = 0; index < 100; index++)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    if (!await scope.ServiceProvider.GetRequiredService<BookingLifecycleRelay>().DispatchOneAsync(stoppingToken)) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Booking lifecycle worker failed; persisted events remain pending."); }
        }
    }
}
