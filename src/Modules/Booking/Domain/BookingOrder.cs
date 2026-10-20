using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Booking.Domain;

public sealed class BookingOrder : AggregateRoot<Guid>
{
    private BookingOrder() { }
    public Guid MenteeUserId { get; private set; }
    public Guid MentorUserId { get; private set; }
    public ServiceTerms Terms { get; private set; } = null!;
    public IntakeForm Intake { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string BookingCode { get; private set; } = "";
    public string Status { get; private set; } = "Holding";
    public DateTimeOffset HeldUntilUtc { get; private set; }
    public Guid? PaymentId { get; private set; }
    public string? MentorNote { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public bool NeedsSlotRelease { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public List<BookingSession> Sessions { get; private set; } = [];
    public List<BookingAudit> AuditLogs { get; private set; } = [];
    public List<RescheduleRequest> RescheduleRequests { get; private set; } = [];

    public static Result<BookingOrder> Create(Guid mentee, ServiceTerms terms, IntakeForm intake,
        IReadOnlyList<Guid> slots, string key, string hash, DateTimeOffset now)
    {
        var valid = terms.Validate();
        if (valid.IsFailure) return valid.Error;
        valid = intake.Validate();
        if (valid.IsFailure) return valid.Error;
        if (mentee == Guid.Empty || mentee == terms.MentorUserId || slots.Count != terms.TotalSessions ||
            slots.Any(x => x == Guid.Empty) || slots.Distinct().Count() != slots.Count ||
            string.IsNullOrWhiteSpace(key) || key.Length > 100 || key.Any(x => x is < '!' or > '~') || hash.Length != 64)
            return Error.Validation("Booking.InvalidPackage", "Phải chọn đúng số slot của gói và không tự đặt dịch vụ của mình.");
        var id = Guid.CreateVersion7();
        var order = new BookingOrder
        {
            Id = id,
            MenteeUserId = mentee,
            MentorUserId = terms.MentorUserId,
            Terms = terms,
            Intake = intake,
            IdempotencyKey = key,
            RequestHash = hash,
            BookingCode = $"SB-{now.Year}-{id:N}",
            HeldUntilUtc = now.AddMinutes(10),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Sessions = slots.Select((slot, index) => BookingSession.Create(id, slot, index + 1)).ToList()
        };
        order.AuditLogs.Add(BookingAudit.Create(id, null, "Holding", mentee, "Khởi tạo giữ chỗ cả gói", now));
        return order;
    }

    public Result FinishHold(IReadOnlyList<SessionWindow> windows, DateTimeOffset until, DateTimeOffset now)
    {
        if (Status == "PendingPayment") return Result.Success();
        if (Status != "Holding" || until <= now || until > HeldUntilUtc || windows.Count != Sessions.Count ||
            windows.Select(x => x.SlotId).Distinct().Count() != Sessions.Count ||
            windows.Any(x => !Sessions.Any(s => s.SlotId == x.SlotId) || x.Start <= until ||
                x.End - x.Start != TimeSpan.FromMinutes(Terms.DurationMinutes)))
            return Result.Failure(Error.Conflict("Booking.InvalidReservation", "Không giữ đủ lịch phù hợp cho gói."));
        var ordered = windows.OrderBy(x => x.Start).ToArray();
        if (ordered.Zip(ordered.Skip(1)).Any(x => x.First.End > x.Second.Start))
            return Result.Failure(Error.Conflict("Booking.OverlappingSessions", "Các buổi trong gói không được chồng giờ."));
        for (var index = 0; index < ordered.Length; index++)
        {
            var window = ordered[index];
            Sessions.Single(x => x.SlotId == window.SlotId).Schedule(index + 1, window.Start, window.End);
        }
        HeldUntilUtc = until;
        ChangeStatus("PendingPayment", MenteeUserId, "Đã giữ đủ slot", now);
        return Result.Success();
    }

    public Result ReceivePayment(Guid paymentId, decimal amount, string currency, DateTimeOffset now)
    {
        if (PaymentId == paymentId && paymentId != Guid.Empty && amount == Terms.Price && currency == Terms.Currency) return Result.Success();
        if (Status != "PendingPayment" || HeldUntilUtc <= now || PaymentId.HasValue || paymentId == Guid.Empty ||
            amount != Terms.Price || currency != Terms.Currency)
            return Result.Failure(Error.Conflict("Booking.PaymentMismatch", "Thanh toán không khớp gói hoặc booking đã hết hạn."));
        PaymentId = paymentId;
        ChangeStatus("AwaitingMentor", null, "Đã xác minh thanh toán, chờ Mentor xác nhận", now);
        return Result.Success();
    }

    public Result Confirm(Guid mentorUser, string? note, DateTimeOffset now)
    {
        if (mentorUser != Terms.MentorUserId) return Result.Failure(Error.NotFound("Booking.NotFound", "Không tìm thấy booking."));
        if (note is { Length: > 2000 } || note?.Any(char.IsControl) == true)
            return Result.Failure(Error.Validation("Booking.InvalidNote", "Ghi chú tối đa 2000 ký tự, không chứa ký tự điều khiển."));
        if (Status == "Confirmed") return Result.Success();
        if (Status != "AwaitingMentor" || !PaymentId.HasValue || Sessions.Any(x => x.StartAtUtc <= now))
            return Result.Failure(Error.Conflict("Booking.NotPaid", "Booking phải được thanh toán xác minh và chưa bắt đầu."));
        MentorNote = note?.Trim();
        ChangeStatus("Confirmed", mentorUser, "Mentor chấp nhận gói", now);
        return Result.Success();
    }

    public Result Cancel(Guid actor, string reason, DateTimeOffset now)
    {
        if (actor != MenteeUserId && actor != Terms.MentorUserId)
            return Result.Failure(Error.NotFound("Booking.NotFound", "Không tìm thấy booking."));
        if (!IntakeForm.Text(reason, 2000)) return Result.Failure(Error.Validation("Booking.InvalidReason", "Lý do bắt buộc và tối đa 2000 ký tự."));
        if (Status is "Cancelled" or "RefundReview") return Result.Success();
        if (RescheduleRequests.Any(x => x.Status == "Applying"))
            return Result.Failure(Error.Conflict("Booking.Rescheduling", "Dời lịch đang được áp dụng; hãy thử lại sau."));
        if (Status == "Completed" || Sessions.Any(x => x.StartAtUtc.HasValue && x.StartAtUtc <= now) ||
            actor == MenteeUserId && PaymentId.HasValue && Sessions.Any(x => x.StartAtUtc < now.AddHours(24)))
            return Result.Failure(Error.Conflict("Booking.CancellationClosed", "Không thể hủy sau khi bắt đầu; Mentee hủy gói đã trả tiền phải trước ít nhất 24 giờ."));
        CancellationReason = reason.Trim(); CancelledBy = actor; NeedsSlotRelease = true;
        foreach (var pending in RescheduleRequests.Where(x => x.Status == "Pending")) pending.Finish(false, now);
        ChangeStatus(PaymentId.HasValue ? "RefundReview" : "Cancelled", actor, CancellationReason, now);
        return Result.Success();
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status is not ("Holding" or "PendingPayment") || HeldUntilUtc > now) return;
        NeedsSlotRelease = true;
        ChangeStatus("Cancelled", null, "Hết hạn giữ chỗ", now);
    }

    public void RejectHold(string reason, DateTimeOffset now)
    {
        if (Status != "Holding") return;
        NeedsSlotRelease = true;
        ChangeStatus("Cancelled", null, reason, now);
    }

    public void SlotsReleased() => NeedsSlotRelease = false;

    public Result Reschedule(RescheduleRequest request, SessionWindow window, DateTimeOffset now)
    {
        var session = Sessions.SingleOrDefault(x => x.Id == request.BookingSessionId);
        if (Status != "Confirmed" || request.BookingId != Id || request.Status != "Applying" || session is null ||
            session.SlotId != request.OldSlotId || session.StartAtUtc <= request.UpdatedAtUtc || window.SlotId != request.NewSlotId ||
            window.Start <= request.UpdatedAtUtc || window.End - window.Start != TimeSpan.FromMinutes(Terms.DurationMinutes) ||
            Sessions.Any(x => x.Id != session.Id && (x.SlotId == window.SlotId || x.StartAtUtc < window.End && x.EndAtUtc > window.Start)))
            return Result.Failure(Error.Conflict("Booking.RescheduleChanged", "Buổi học không còn phù hợp để dời lịch."));
        session.Move(window.SlotId, window.Start, window.End);
        request.Finish(true, now);
        AuditLogs.Add(BookingAudit.Create(Id, Status, Status, null, $"Dời buổi {session.Id} theo yêu cầu {request.Id}", now));
        UpdatedAtUtc = now;
        return Result.Success();
    }

    public Result Complete(Guid actor, IReadOnlyCollection<Guid> completedSessionIds, DateTimeOffset now)
    {
        if (actor != MenteeUserId && actor != Terms.MentorUserId)
            return Result.Failure(Error.NotFound("Booking.NotFound", "Không tìm thấy booking."));
        if (Status == "Completed") return Result.Success();
        if (Status != "Confirmed" || RescheduleRequests.Any(x => x.Status == "Applying") || Sessions.Any(x => !completedSessionIds.Contains(x.Id) || x.EndAtUtc > now))
            return Result.Failure(Error.Conflict("Booking.SessionsIncomplete", "Phải hoàn thành đủ các buổi của gói trước khi hoàn tất booking."));
        ChangeStatus("Completed", actor, "Đã xác minh hoàn thành đủ buổi", now);
        return Result.Success();
    }

    private void ChangeStatus(string next, Guid? actor, string reason, DateTimeOffset now)
    {
        AuditLogs.Add(BookingAudit.Create(Id, Status, next, actor, reason, now));
        Status = next; UpdatedAtUtc = now;
    }
}

public sealed record SessionWindow(Guid SlotId, DateTimeOffset Start, DateTimeOffset End);

public sealed class BookingSession : AggregateRoot<Guid>
{
    private BookingSession() { }
    public Guid BookingId { get; private set; }
    public Guid SlotId { get; private set; }
    public int SessionNumber { get; private set; }
    public DateTimeOffset? StartAtUtc { get; private set; }
    public DateTimeOffset? EndAtUtc { get; private set; }
    internal static BookingSession Create(Guid booking, Guid slot, int number) => new()
    { Id = Guid.CreateVersion7(), BookingId = booking, SlotId = slot, SessionNumber = number };
    internal void Schedule(int number, DateTimeOffset start, DateTimeOffset end)
    { SessionNumber = number; StartAtUtc = start; EndAtUtc = end; }
    internal void Move(Guid slot, DateTimeOffset start, DateTimeOffset end) { SlotId = slot; StartAtUtc = start; EndAtUtc = end; }
}

public sealed class BookingAudit : AggregateRoot<Guid>
{
    private BookingAudit() { }
    public Guid BookingId { get; private set; }
    public string? OldStatus { get; private set; }
    public string NewStatus { get; private set; } = "";
    public Guid? ChangedByUserId { get; private set; }
    public string Reason { get; private set; } = "";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    internal static BookingAudit Create(Guid booking, string? old, string next, Guid? actor, string reason, DateTimeOffset now) => new()
    { Id = Guid.CreateVersion7(), BookingId = booking, OldStatus = old, NewStatus = next, ChangedByUserId = actor, Reason = reason, CreatedAtUtc = now };
}
