using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Scheduling.Domain;

public sealed class TimeOff : AggregateRoot<Guid>
{
    private TimeOff() { }
    public Guid MentorId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<TimeOff> Create(Guid mentorId, DateOnly start, DateOnly end, string? reason, DateTimeOffset now)
    {
        if (mentorId == Guid.Empty || start > end || end.DayNumber - start.DayNumber > 365 ||
            start.Year < 2000 || end.Year > 9998 || reason?.Length > 1000 || reason?.Any(char.IsControl) == true)
            return Error.Validation("Scheduling.InvalidTimeOff", "Khoảng ngày nghỉ hoặc lý do không hợp lệ.");
        return new TimeOff
        {
            Id = Guid.CreateVersion7(),
            MentorId = mentorId,
            StartDate = start,
            EndDate = end,
            Reason = reason?.Trim(),
            CreatedAtUtc = now
        };
    }
}
