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
        SchedulingSettingsValues values,
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
        settings.Apply(values, now);

        settings.UpdatedAtUtc = null;
        return settings;
    }

    public void Update(SchedulingSettingsValues values, DateTimeOffset now) => Apply(values, now);

    private void Apply(SchedulingSettingsValues values, DateTimeOffset now)
    {
        SessionDurationMinutes = values.SessionDurationMinutes;
        BufferTimeMinutes = values.BufferTimeMinutes;
        LeadTimeHours = values.LeadTimeHours;
        MaxDaysInAdvance = values.MaxDaysInAdvance;
        Timezone = values.Timezone;
        UpdatedAtUtc = now;
    }
}

public sealed record SchedulingSettingsValues
{
    private SchedulingSettingsValues(
        int sessionDurationMinutes,
        int bufferTimeMinutes,
        int leadTimeHours,
        int maxDaysInAdvance,
        string timezone)
    {
        SessionDurationMinutes = sessionDurationMinutes;
        BufferTimeMinutes = bufferTimeMinutes;
        LeadTimeHours = leadTimeHours;
        MaxDaysInAdvance = maxDaysInAdvance;
        Timezone = timezone;
    }

    public int SessionDurationMinutes { get; }
    public int BufferTimeMinutes { get; }
    public int LeadTimeHours { get; }
    public int MaxDaysInAdvance { get; }
    public string Timezone { get; }

    public static Result<SchedulingSettingsValues> Create(
        int sessionDurationMinutes,
        int bufferTimeMinutes,
        int leadTimeHours,
        int maxDaysInAdvance,
        string? timezone)
    {
        var validationError = SchedulingSettingsRules.Validate(
            sessionDurationMinutes,
            bufferTimeMinutes,
            leadTimeHours,
            maxDaysInAdvance,
            timezone);
        if (validationError != Error.None)
        {
            return validationError;
        }

        return new SchedulingSettingsValues(
            sessionDurationMinutes,
            bufferTimeMinutes,
            leadTimeHours,
            maxDaysInAdvance,
            timezone!.Trim());
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
        if (!IsValidSessionDuration(sessionDurationMinutes))
        {
            return Error.Validation(
                "Scheduling.InvalidSessionDuration",
                "Thời lượng buổi học phải từ 15 đến 240 phút và là bội số của 15.");
        }

        if (!IsValidBufferTime(bufferTimeMinutes))
        {
            return Error.Validation(
                "Scheduling.InvalidBufferTime",
                "Thời gian nghỉ phải từ 0 đến 120 phút và là bội số của 5.");
        }

        if (!IsValidLeadTime(leadTimeHours))
        {
            return Error.Validation(
                "Scheduling.InvalidLeadTime",
                "Thời gian đặt trước phải từ 0 đến 720 giờ.");
        }

        if (!IsValidAdvanceWindow(maxDaysInAdvance))
        {
            return Error.Validation(
                "Scheduling.InvalidAdvanceWindow",
                "Số ngày cho phép đặt trước phải từ 1 đến 365 ngày.");
        }

        if (!IsValidBookingWindow(leadTimeHours, maxDaysInAdvance))
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

    public static bool IsValidSessionDuration(int value) =>
        value is >= 15 and <= 240 && value % 15 == 0;

    public static bool IsValidBufferTime(int value) =>
        value is >= 0 and <= 120 && value % 5 == 0;

    public static bool IsValidLeadTime(int value) => value is >= 0 and <= 720;

    public static bool IsValidAdvanceWindow(int value) => value is >= 1 and <= 365;

    public static bool IsValidBookingWindow(int leadTimeHours, int maxDaysInAdvance) =>
        leadTimeHours <= maxDaysInAdvance * 24;

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
