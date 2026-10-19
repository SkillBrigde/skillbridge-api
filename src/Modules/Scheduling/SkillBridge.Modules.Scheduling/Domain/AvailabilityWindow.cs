using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Scheduling.Domain;

public sealed record AvailabilityWindow
{
    private AvailabilityWindow(int dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public int DayOfWeek { get; }
    public TimeOnly StartTime { get; }
    public TimeOnly EndTime { get; }

    public static bool IsValidDay(int dayOfWeek) => dayOfWeek is >= 0 and <= 6;
    public static bool IsWholeSecond(TimeOnly time) => time.Ticks % TimeSpan.TicksPerSecond == 0;

    public static Result<AvailabilityWindow> Create(int dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        if (!IsValidDay(dayOfWeek))
        {
            return Error.Validation("Scheduling.InvalidDayOfWeek", "Ngày trong tuần phải từ 0 (Chủ nhật) đến 6 (Thứ bảy).");
        }

        if (!IsWholeSecond(startTime) || !IsWholeSecond(endTime))
        {
            return Error.Validation("Scheduling.InvalidTimePrecision", "Giờ bắt đầu và kết thúc phải có độ chính xác đến giây, không nhận phần lẻ giây.");
        }

        if (startTime >= endTime)
        {
            return Error.Validation("Scheduling.InvalidTimeRange", "Giờ bắt đầu phải nhỏ hơn giờ kết thúc trong cùng một ngày.");
        }

        return new AvailabilityWindow(dayOfWeek, startTime, endTime);
    }
}
