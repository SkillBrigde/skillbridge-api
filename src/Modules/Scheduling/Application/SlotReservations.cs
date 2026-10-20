using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Modules.Scheduling.Application;

public sealed record ReservedSlot(Guid SlotId, DateTimeOffset StartAtUtc, DateTimeOffset EndAtUtc);
public sealed record SlotReservation(Guid BookingId, Guid MentorId, DateTimeOffset HeldUntilUtc, IReadOnlyList<ReservedSlot> Slots);

// Application port dành cho adapter Booking tại Host; không có endpoint công khai tự giữ/confirm slot.
public sealed class SlotReservations(SchedulingDbContext db, SlotStore store, TimeProvider clock)
{
    public async Task<Result<SlotReservation>> HoldAsync(Guid mentorId, Guid bookingId, IReadOnlyList<Guid> slotIds,
        int durationMinutes, DateTimeOffset until, CancellationToken ct)
    {
        if (mentorId == Guid.Empty || bookingId == Guid.Empty || slotIds.Count is < 1 or > 52 ||
            slotIds.Any(x => x == Guid.Empty) || slotIds.Distinct().Count() != slotIds.Count || durationMinutes is < 15 or > 240)
            return Error.Validation("Scheduling.InvalidReservation", "Danh sách slot giữ cho gói không hợp lệ.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await store.LockAsync(mentorId, ct);
        var generated = await store.RegenerateAsync(mentorId, ct);
        if (generated.IsFailure) return generated.Error;
        var now = clock.GetUtcNow();
        var settings = await db.SchedulingSettings.SingleAsync(x => x.MentorId == mentorId, ct);
        var slots = await db.Slots.Where(x => x.MentorId == mentorId && slotIds.Contains(x.Id)).OrderBy(x => x.StartAtUtc).ToListAsync(ct);
        if (slots.Count != slotIds.Count) return Error.NotFound("Scheduling.SlotNotFound", "Không tìm thấy đủ slot của Mentor.");
        if (await db.Slots.AnyAsync(x => x.MentorId == mentorId && x.BookingId == bookingId && !slotIds.Contains(x.Id), ct))
            return Error.Conflict("Scheduling.ReservationChanged", "Booking đã giữ một tập slot khác.");
        if (until <= now || until > now.AddMinutes(10)) return Error.Validation("Scheduling.InvalidHold", "Thời hạn giữ chỗ tối đa 10 phút.");
        if (slots.Any(x => x.StartAtUtc <= now || x.StartAtUtc < now.AddHours(settings.LeadTimeHours) ||
            x.StartAtUtc < until || x.EndAtUtc - x.StartAtUtc != TimeSpan.FromMinutes(durationMinutes)))
            return Error.Conflict("Scheduling.SlotTermsChanged", "Slot không còn phù hợp thời gian đặt trước hoặc thời lượng dịch vụ.");
        foreach (var slot in slots)
        {
            var held = slot.Hold(bookingId, until, now);
            if (held.IsFailure) return held.Error;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new SlotReservation(bookingId, mentorId, slots.Min(x => x.HeldUntilUtc)!.Value,
            slots.Select(x => new ReservedSlot(x.Id, x.StartAtUtc, x.EndAtUtc)).ToList());
    }

    public async Task<Result> ConfirmAsync(Guid mentorId, Guid bookingId, int expectedCount, CancellationToken ct)
    {
        if (bookingId == Guid.Empty || mentorId == Guid.Empty || expectedCount is < 1 or > 52)
            return Result.Failure(Error.Validation("Scheduling.InvalidReservation", "Booking và số buổi không hợp lệ."));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await store.LockAsync(mentorId, ct);
        var slots = await db.Slots.Where(x => x.MentorId == mentorId && x.BookingId == bookingId).ToListAsync(ct);
        if (slots.Count != expectedCount) return Result.Failure(Error.Conflict("Scheduling.ReservationLost", "Booking không còn đủ slot giữ chỗ."));
        foreach (var slot in slots)
        {
            var confirmed = slot.Confirm(bookingId, clock.GetUtcNow());
            if (confirmed.IsFailure) return confirmed;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReleaseAsync(Guid mentorId, Guid bookingId, CancellationToken ct)
    {
        if (bookingId == Guid.Empty || mentorId == Guid.Empty)
            return Result.Failure(Error.Validation("Scheduling.InvalidReservation", "Booking không hợp lệ."));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await store.LockAsync(mentorId, ct);
        var slots = await db.Slots.Where(x => x.MentorId == mentorId && x.BookingId == bookingId).ToListAsync(ct);
        if (slots.Count == 0)
        {
            await transaction.CommitAsync(ct);
            return Result.Success();
        }
        foreach (var slot in slots) slot.Release(bookingId, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        var generated = await store.RegenerateAsync(mentorId, ct);
        if (generated.IsFailure) return generated;
        await transaction.CommitAsync(ct);
        return Result.Success();
    }

    public async Task<Result<ReservedSlot>> MoveBookedAsync(Guid mentorId, Guid bookingId, Guid oldSlotId, Guid newSlotId,
        int durationMinutes, CancellationToken ct)
    {
        if (mentorId == Guid.Empty || bookingId == Guid.Empty || oldSlotId == Guid.Empty || newSlotId == Guid.Empty || oldSlotId == newSlotId)
            return Error.Validation("Scheduling.InvalidMove", "Định danh dời lịch không hợp lệ.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await store.LockAsync(mentorId, ct);
        var generated = await store.RegenerateAsync(mentorId, ct);
        if (generated.IsFailure) return generated.Error;
        var old = await db.Slots.SingleOrDefaultAsync(x => x.Id == oldSlotId && x.MentorId == mentorId, ct);
        var next = await db.Slots.SingleOrDefaultAsync(x => x.Id == newSlotId && x.MentorId == mentorId, ct);
        if (old is null || next is null) return Error.NotFound("Scheduling.SlotNotFound", "Không tìm thấy slot dời lịch.");
        if (next.Status == "Booked" && next.BookingId == bookingId && old.BookingId != bookingId)
            return new ReservedSlot(next.Id, next.StartAtUtc, next.EndAtUtc);
        var now = clock.GetUtcNow();
        var settings = await db.SchedulingSettings.SingleAsync(x => x.MentorId == mentorId, ct);
        if (old.Status != "Booked" || old.BookingId != bookingId || old.StartAtUtc <= now || next.StartAtUtc <= now ||
            next.StartAtUtc < now.AddHours(settings.LeadTimeHours) || next.EndAtUtc - next.StartAtUtc != TimeSpan.FromMinutes(durationMinutes))
            return Error.Conflict("Scheduling.CannotMove", "Slot cũ hoặc slot mới không đáp ứng điều kiện dời lịch.");
        var until = next.StartAtUtc < now.AddMinutes(10) ? next.StartAtUtc : now.AddMinutes(10);
        var held = next.Hold(bookingId, until, now);
        if (held.IsFailure) return held.Error;
        var confirmed = next.Confirm(bookingId, now);
        if (confirmed.IsFailure) return confirmed.Error;
        old.Release(bookingId, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new ReservedSlot(next.Id, next.StartAtUtc, next.EndAtUtc);
    }
}
