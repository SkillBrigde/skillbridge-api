using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Scheduling.Domain;

public sealed class AvailabilitySlot : AggregateRoot<Guid>
{
    private AvailabilitySlot() { }
    public Guid MentorId { get; private set; }
    public DateTimeOffset StartAtUtc { get; private set; }
    public DateTimeOffset EndAtUtc { get; private set; }
    public string Status { get; private set; } = "Available";
    public Guid? BookingId { get; private set; }
    public DateTimeOffset? HeldUntilUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AvailabilitySlot Create(Guid mentorId, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now) => new()
    { Id = Guid.CreateVersion7(), MentorId = mentorId, StartAtUtc = start, EndAtUtc = end, CreatedAtUtc = now };

    public bool IsProtected(DateTimeOffset now) => Status == "Booked" || Status == "Held" && HeldUntilUtc > now;

    public void SetAvailability(string status, DateTimeOffset now)
    {
        if (IsProtected(now)) return;
        Status = status;
        BookingId = null;
        HeldUntilUtc = null;
    }

    public Result Hold(Guid bookingId, DateTimeOffset until, DateTimeOffset now)
    {
        if (bookingId == Guid.Empty || until <= now || until > StartAtUtc)
            return Result.Failure(Error.Validation("Scheduling.InvalidHold", "Thời hạn giữ slot không hợp lệ."));
        if (Status == "Held" && BookingId == bookingId && HeldUntilUtc > now) return Result.Success();
        if (Status != "Available" && !(Status == "Held" && HeldUntilUtc <= now))
            return Result.Failure(Error.Conflict("Scheduling.SlotUnavailable", "Slot đã được giữ hoặc không còn khả dụng."));
        Status = "Held"; BookingId = bookingId; HeldUntilUtc = until;
        return Result.Success();
    }

    public Result Confirm(Guid bookingId, DateTimeOffset now)
    {
        if (Status == "Booked" && BookingId == bookingId) return Result.Success();
        if (Status != "Held" || BookingId != bookingId || HeldUntilUtc <= now)
            return Result.Failure(Error.Conflict("Scheduling.HoldExpired", "Slot không còn được giữ cho booking này."));
        Status = "Booked"; HeldUntilUtc = null;
        return Result.Success();
    }

    public void Release(Guid bookingId, DateTimeOffset now)
    {
        if (BookingId != bookingId) return;
        Status = "Available"; BookingId = null; HeldUntilUtc = null;
    }
}
