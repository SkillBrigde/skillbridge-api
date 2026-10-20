using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Application.DTOs;
using SkillBridge.Modules.Booking.Domain;
using SkillBridge.Modules.Booking.Infrastructure;

namespace SkillBridge.Modules.Booking.Application;

public sealed class Reschedules(BookingDbContext db, Bookings bookings, IBookingSlotReservations slots, TimeProvider clock)
{
    public async Task<Result<RescheduleResponse>> CreateAsync(Guid bookingId, Guid actor, CreateRescheduleRequest input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await bookings.LockAsync(bookingId.ToString(), ct);
        var order = await bookings.LoadAsync(bookingId, ct);
        if (order is null || actor != order.MenteeUserId && actor != order.MentorUserId) return BookingErrors.Missing;
        if (order.RescheduleRequests.Count >= 100 || order.RescheduleRequests.Any(x =>
            x.Status == "Applying" || x.BookingSessionId == input.BookingSessionId && x.Status == "Pending"))
            return Error.Conflict("Booking.ReschedulePending", "Đã có yêu cầu đang xử lý hoặc vượt 100 yêu cầu cho booking.");
        var created = RescheduleRequest.Create(order, input.BookingSessionId, input.NewSlotId, actor, input.Reason, clock.GetUtcNow());
        if (created.IsFailure) return created.Error;
        db.RescheduleRequests.Add(created.Value);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return RescheduleResponse.From(created.Value);
    }

    public async Task<Result> RespondAsync(Guid id, Guid actor, RespondRescheduleRequest input, CancellationToken ct)
    {
        if (!input.IsApproved.HasValue) return Result.Failure(Error.Validation("Booking.MissingApproval", "isApproved là trường bắt buộc."));
        var bookingId = await db.RescheduleRequests.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.BookingId).SingleOrDefaultAsync(ct);
        if (!bookingId.HasValue) return Result.Failure(BookingErrors.Missing);
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await bookings.LockAsync(bookingId.Value.ToString(), ct);
            var order = await bookings.LoadAsync(bookingId.Value, ct);
            if (order is null || actor != order.MenteeUserId && actor != order.MentorUserId) return Result.Failure(BookingErrors.Missing);
            var request = order.RescheduleRequests.Single(x => x.Id == id);
            if (order.Status != "Confirmed" || order.RescheduleRequests.Any(x => x.Id != id && x.Status == "Applying"))
                return Result.Failure(Error.Conflict("Booking.RescheduleClosed", "Booking không còn cho phép dời lịch hoặc đang áp dụng yêu cầu khác."));
            var result = request.Respond(actor, input.IsApproved.Value, input.ResponseNote, clock.GetUtcNow());
            if (result.IsFailure) return result;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        db.ChangeTracker.Clear();
        return input.IsApproved.Value ? await ApplyAsync(id, ct) : Result.Success();
    }

    internal async Task<Result> ApplyAsync(Guid id, CancellationToken ct)
    {
        var bookingId = await db.RescheduleRequests.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.BookingId).SingleOrDefaultAsync(ct);
        if (!bookingId.HasValue) return Result.Failure(BookingErrors.Missing);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await bookings.LockAsync(bookingId.Value.ToString(), ct);
        var order = await bookings.LoadAsync(bookingId.Value, ct);
        if (order is null) return Result.Failure(BookingErrors.Missing);
        var request = order.RescheduleRequests.Single(x => x.Id == id);
        if (request.Status != "Applying") return Result.Success();
        var session = order.Sessions.Single(x => x.Id == request.BookingSessionId);
        if (order.Status != "Confirmed" || session.SlotId != request.OldSlotId)
        {
            request.Finish(false, clock.GetUtcNow());
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Result.Failure(Error.Conflict("Booking.RescheduleChanged", "Buổi học đã thay đổi."));
        }
        var moved = await slots.MoveAsync(order.Terms.MentorId, order.Id, request.OldSlotId, request.NewSlotId, order.Terms.DurationMinutes, ct);
        if (moved.IsFailure)
        {
            if (moved.Error.Type == ErrorType.Failure) return Result.Failure(moved.Error);
            request.Finish(false, clock.GetUtcNow());
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Result.Failure(moved.Error);
        }
        var applied = order.Reschedule(request, moved.Value, clock.GetUtcNow());
        if (applied.IsFailure) return applied;
        db.OutboxMessages.Add(new OutboxMessage { Type = "Booking.Rescheduled.v1", Content = JsonSerializer.Serialize(BookingResponse.From(order)), OccurredOnUtc = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Result.Success();
    }
}
