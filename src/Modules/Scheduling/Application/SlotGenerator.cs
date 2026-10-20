using SkillBridge.Modules.Scheduling.Domain;

namespace SkillBridge.Modules.Scheduling.Application;

public readonly record struct SlotWindow(DateTimeOffset Start, DateTimeOffset End);

public static class SlotGenerator
{
    public static IReadOnlyList<SlotWindow> Generate(SchedulingSettings settings, IReadOnlyCollection<AvailabilityRule> rules, DateOnly firstDay)
    {
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone);
        List<SlotWindow> slots = [];
        for (var day = firstDay; day < firstDay.AddDays(settings.MaxDaysInAdvance); day = day.AddDays(1))
        {
            foreach (var rule in rules.Where(x => x.IsActive && x.DayOfWeek == (int)day.DayOfWeek).OrderBy(x => x.StartTime))
            {
                var start = day.ToDateTime(rule.StartTime, DateTimeKind.Unspecified);
                var limit = day.ToDateTime(rule.EndTime, DateTimeKind.Unspecified);
                while (start.AddMinutes(settings.SessionDurationMinutes) <= limit)
                {
                    var end = start.AddMinutes(settings.SessionDurationMinutes);
                    var utcStart = ToUtc(start, timezone);
                    var utcEnd = ToUtc(end, timezone);
                    if (utcStart.HasValue && utcEnd.HasValue && utcEnd - utcStart == TimeSpan.FromMinutes(settings.SessionDurationMinutes) &&
                        (slots.Count == 0 || utcStart >= slots[^1].End.AddMinutes(settings.BufferTimeMinutes)))
                        slots.Add(new(utcStart.Value, utcEnd.Value));
                    start = end.AddMinutes(settings.BufferTimeMinutes);
                }
            }
        }
        return slots;
    }

    private static DateTimeOffset? ToUtc(DateTime local, TimeZoneInfo timezone)
    {
        if (timezone.IsInvalidTime(local)) return null;
        // Offset lớn hơn tương ứng lần xuất hiện sớm hơn khi đồng hồ lặp giờ.
        var offset = timezone.IsAmbiguousTime(local) ? timezone.GetAmbiguousTimeOffsets(local).Max() : timezone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
