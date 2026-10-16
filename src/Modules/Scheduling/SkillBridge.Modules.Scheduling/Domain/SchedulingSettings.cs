using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Scheduling.Domain;

public sealed class SchedulingSettings : AggregateRoot<Guid>
{
    private SchedulingSettings() { }

    public Guid MentorId { get; private set; }
    public int SessionDurationMinutes { get; private set; }
    public int BufferTimeMinutes { get; private set; }
    public int LeadTimeHours { get; private set; }
    public int MaxDaysInAdvance { get; private set; }
    public string Timezone { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static Result<SchedulingSettings> Create(
        Guid mentorId,
        int sessionDurationMinutes,
        int bufferTimeMinutes,
        int leadTimeHours,
        int maxDaysInAdvance,
        string? timezone,
        DateTimeOffset now)
    {
        if (mentorId == Guid.Empty)
        {
            return Error.Validation("Scheduling.InvalidMentorId", "Mã Mentor không hợp lệ.");
        }

        var settings = new SchedulingSettings
        {
            Id = Guid.CreateVersion7(),
            MentorId = mentorId,
            CreatedAtUtc = now
        };
        var updated = settings.Update(
            sessionDurationMinutes,
            bufferTimeMinutes,
            leadTimeHours,
            maxDaysInAdvance,
            timezone,
            now);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        settings.UpdatedAtUtc = null;
        return settings;
    }

    public Result Update(
        int sessionDurationMinutes,
        int bufferTimeMinutes,
        int leadTimeHours,
        int maxDaysInAdvance,
        string? timezone,
        DateTimeOffset now)
    {
        var validationError = SchedulingSettingsRules.Validate(
            sessionDurationMinutes,
            bufferTimeMinutes,
            leadTimeHours,
            maxDaysInAdvance,
            timezone);
        if (validationError != Error.None)
        {
            return Result.Failure(validationError);
        }

        SessionDurationMinutes = sessionDurationMinutes;
        BufferTimeMinutes = bufferTimeMinutes;
        LeadTimeHours = leadTimeHours;
        MaxDaysInAdvance = maxDaysInAdvance;
        Timezone = timezone!.Trim();
        UpdatedAtUtc = now;
        return Result.Success();
    }
}

internal static class SchedulingSettingsRules
{
    public static Error Validate(
        int sessionDurationMinutes,
        int bufferTimeMinutes,
        int leadTimeHours,
        int maxDaysInAdvance,
        string? timezone)
    {
        if (sessionDurationMinutes is < 15 or > 240 || sessionDurationMinutes % 15 != 0)
        {
            return Error.Validation(
                "Scheduling.InvalidSessionDuration",
                "Thời lượng buổi học phải từ 15 đến 240 phút và là bội số của 15.");
        }

        if (bufferTimeMinutes is < 0 or > 120 || bufferTimeMinutes % 5 != 0)
        {
            return Error.Validation(
                "Scheduling.InvalidBufferTime",
                "Thời gian nghỉ phải từ 0 đến 120 phút và là bội số của 5.");
        }

        if (leadTimeHours is < 0 or > 720)
        {
            return Error.Validation(
                "Scheduling.InvalidLeadTime",
                "Thời gian đặt trước phải từ 0 đến 720 giờ.");
        }

        if (maxDaysInAdvance is < 1 or > 365)
        {
            return Error.Validation(
                "Scheduling.InvalidAdvanceWindow",
                "Số ngày cho phép đặt trước phải từ 1 đến 365 ngày.");
        }

        if (leadTimeHours > maxDaysInAdvance * 24)
        {
            return Error.Validation(
                "Scheduling.InvalidBookingWindow",
                "Thời gian đặt trước tối thiểu không được vượt quá cửa sổ cho phép đặt lịch.");
        }

        if (!IsValidTimezone(timezone))
        {
            return Error.Validation(
                "Scheduling.InvalidTimezone",
                "Múi giờ phải là mã IANA hợp lệ, ví dụ Asia/Ho_Chi_Minh, hoặc UTC.");
        }

        return Error.None;
    }

    public static bool IsValidTimezone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone) || timezone.Length > 100 || timezone.Any(char.IsControl))
        {
            return false;
        }

        var normalized = timezone.Trim();
        if (normalized != "UTC" && !normalized.Contains('/'))
        {
            return false;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(normalized, out _);
    }
}
