using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Learning.Domain;

public sealed class LearningSession : AggregateRoot<Guid>
{
    private LearningSession() { }
    public Guid BookingId { get; private set; }
    public Guid BookingSessionId { get; private set; }
    public Guid MentorId { get; private set; }
    public Guid MentorUserId { get; private set; }
    public Guid MenteeUserId { get; private set; }
    public int SessionNumber { get; private set; }
    public string Status { get; private set; } = "Scheduled";
    public string? RoomUrl { get; private set; }
    public DateTimeOffset ScheduledStartUtc { get; private set; }
    public DateTimeOffset ScheduledEndUtc { get; private set; }
    public DateTimeOffset? ActualStartUtc { get; private set; }
    public DateTimeOffset? ActualEndUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<LearningSession> Create(Guid bookingId, Guid bookingSessionId, Guid mentorId, Guid mentorUserId,
        Guid menteeUserId, int number, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    {
        if (bookingId == Guid.Empty || bookingSessionId == Guid.Empty || mentorId == Guid.Empty || mentorUserId == Guid.Empty ||
            menteeUserId == Guid.Empty || mentorUserId == menteeUserId || number is < 1 or > 52 || end <= start || end - start > TimeSpan.FromHours(4))
            return Error.Validation("Learning.InvalidSession", "Thông tin buổi học từ Booking không hợp lệ.");
        return new LearningSession
        {
            Id = Guid.CreateVersion7(),
            BookingId = bookingId,
            BookingSessionId = bookingSessionId,
            MentorId = mentorId,
            MentorUserId = mentorUserId,
            MenteeUserId = menteeUserId,
            SessionNumber = number,
            ScheduledStartUtc = start,
            ScheduledEndUtc = end,
            CreatedAtUtc = now
        };
    }

    public bool IsParticipant(Guid user) => user == MentorUserId || user == MenteeUserId;

    public Result Sync(DateTimeOffset start, DateTimeOffset end, bool cancelled)
    {
        if (cancelled)
        {
            if (Status != "Completed") Status = "Cancelled";
            return Result.Success();
        }
        if (start == ScheduledStartUtc && end == ScheduledEndUtc) return Result.Success();
        if (Status != "Scheduled" || end <= start || end - start > TimeSpan.FromHours(4))
            return Result.Failure(Error.Conflict("Learning.ScheduleChanged", "Không được ghi đè lịch buổi đã bắt đầu hoặc kết thúc."));
        ScheduledStartUtc = start; ScheduledEndUtc = end; RoomUrl = null;
        return Result.Success();
    }

    public Result Start(Guid actor, string? roomUrl, DateTimeOffset now)
    {
        if (actor != MentorUserId) return Result.Failure(Error.NotFound("Learning.NotFound", "Không tìm thấy buổi học."));
        if (roomUrl is null || roomUrl.Length > 500 || !Uri.TryCreate(roomUrl, UriKind.Absolute, out var room) ||
            room.Scheme != "https" || room.Host != "meet.google.com" || !room.IsDefaultPort || room.UserInfo.Length > 0 ||
            room.Query.Length > 0 || room.Fragment.Length > 0 || room.AbsolutePath.Length <= 1 ||
            room.AbsolutePath[1..].Any(x => !char.IsAsciiLetterOrDigit(x) && x != '-'))
            return Result.Failure(Error.Validation("Learning.InvalidRoom", "Cung cấp link phòng Google Meet HTTPS, không query/fragment."));
        if (Status == "InProgress") { RoomUrl = room.AbsoluteUri; return Result.Success(); }
        if (Status != "Scheduled" || now < ScheduledStartUtc || now >= ScheduledEndUtc)
            return Result.Failure(Error.Conflict("Learning.StartWindow", "Chỉ bắt đầu trong khoảng giờ đã đặt."));
        RoomUrl = room.AbsoluteUri; Status = "InProgress"; ActualStartUtc = now;
        return Result.Success();
    }

    public Result End(Guid actor, IReadOnlyList<SessionAttendance> attendance, DateTimeOffset now)
    {
        if (actor != MentorUserId) return Result.Failure(Error.NotFound("Learning.NotFound", "Không tìm thấy buổi học."));
        if (Status == "Completed") return Result.Success();
        if (Status != "InProgress" || now < ScheduledEndUtc || !ActualStartUtc.HasValue)
            return Result.Failure(Error.Conflict("Learning.EndWindow", "Chỉ kết thúc buổi đang học sau giờ kết thúc đã đặt."));
        var mentor = attendance.Where(x => x.SessionId == Id && x.UserId == MentorUserId);
        var mentee = attendance.Where(x => x.SessionId == Id && x.UserId == MenteeUserId);
        if (!mentor.Any(a => mentee.Any(b => a.JoinedAtUtc < (b.LeftAtUtc ?? now) && b.JoinedAtUtc < (a.LeftAtUtc ?? now))))
            return Result.Failure(Error.Conflict("Learning.AttendanceRequired", "Cần ghi nhận hai bên cùng tham gia trong một khoảng thời gian."));
        ActualEndUtc = now; Status = "Completed";
        foreach (var item in attendance.Where(x => x.SessionId == Id)) item.Leave(now);
        return Result.Success();
    }
}

public sealed class SessionAttendance : AggregateRoot<Guid>
{
    private SessionAttendance() { }
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }
    public DateTimeOffset? LeftAtUtc { get; private set; }
    public static Result<SessionAttendance> Join(LearningSession session, Guid user, DateTimeOffset now)
    {
        if (!session.IsParticipant(user)) return Error.NotFound("Learning.NotFound", "Không tìm thấy buổi học.");
        if (session.Status != "InProgress" || now < session.ScheduledStartUtc || now >= session.ScheduledEndUtc.AddMinutes(30))
            return Error.Conflict("Learning.JoinWindow", "Buổi học chưa bắt đầu hoặc đã đóng cửa điểm danh.");
        return new SessionAttendance { Id = Guid.CreateVersion7(), SessionId = session.Id, UserId = user, JoinedAtUtc = now };
    }
    public void Leave(DateTimeOffset now)
    {
        if (!LeftAtUtc.HasValue && now >= JoinedAtUtc) LeftAtUtc = now;
    }
}
