using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Booking.Domain;

public sealed class RescheduleRequest : AggregateRoot<Guid>
{
    private RescheduleRequest() { }
    public Guid BookingId { get; private set; }
    public Guid BookingSessionId { get; private set; }
    public Guid OldSlotId { get; private set; }
    public Guid NewSlotId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string Reason { get; private set; } = "";
    public string Status { get; private set; } = "Pending";
    public string? ResponseNote { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<RescheduleRequest> Create(BookingOrder order, Guid sessionId, Guid newSlot, Guid actor, string? reason, DateTimeOffset now)
    {
        var session = order.Sessions.SingleOrDefault(x => x.Id == sessionId);
        if (actor != order.MenteeUserId && actor != order.MentorUserId)
            return Error.NotFound("Booking.NotFound", "Không tìm thấy booking.");
        if (session is null || session.StartAtUtc <= now || order.Status != "Confirmed" ||
            newSlot == Guid.Empty || order.Sessions.Any(x => x.SlotId == newSlot) || !IntakeForm.Text(reason, 2000))
            return Error.Validation("Booking.InvalidReschedule", "Chỉ dời buổi chưa bắt đầu của gói đã xác nhận, sang slot mới với lý do hợp lệ.");
        return new RescheduleRequest
        {
            Id = Guid.CreateVersion7(),
            BookingId = order.Id,
            BookingSessionId = sessionId,
            OldSlotId = session.SlotId,
            NewSlotId = newSlot,
            RequestedByUserId = actor,
            Reason = reason!.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public Result Respond(Guid actor, bool approved, string? note, DateTimeOffset now)
    {
        if (actor == RequestedByUserId) return Result.Failure(Error.Forbidden("Booking.SelfApproval", "Bên gửi không được tự duyệt dời lịch."));
        if (note is { Length: > 2000 } || note?.Any(char.IsControl) == true)
            return Result.Failure(Error.Validation("Booking.InvalidNote", "Ghi chú tối đa 2000 ký tự, không chứa ký tự điều khiển."));
        if (Status == (approved ? "Approved" : "Rejected") || approved && Status == "Applying") return Result.Success();
        if (Status != "Pending") return Result.Failure(Error.Conflict("Booking.RescheduleClosed", "Yêu cầu dời lịch đã được xử lý."));
        Status = approved ? "Applying" : "Rejected"; ResponseNote = note?.Trim(); UpdatedAtUtc = now;
        return Result.Success();
    }

    internal void Finish(bool approved, DateTimeOffset now) { Status = approved ? "Approved" : "Rejected"; UpdatedAtUtc = now; }
}
