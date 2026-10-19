using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Scheduling.Domain;

public sealed class AvailabilityRule : AggregateRoot<Guid>
{
    private AvailabilityRule() { }

    public Guid MentorId { get; private set; }
    public int DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static Result<AvailabilityRule> Create(Guid mentorId, AvailabilityWindow window, DateTimeOffset now)
    {
        if (mentorId == Guid.Empty)
        {
            return Error.Validation("Scheduling.InvalidMentorId", "Mã Mentor không hợp lệ.");
        }

        return new AvailabilityRule
        {
            Id = Guid.CreateVersion7(),
            MentorId = mentorId,
            DayOfWeek = window.DayOfWeek,
            StartTime = window.StartTime,
            EndTime = window.EndTime,
            IsActive = true,
            CreatedAtUtc = now
        };
    }
}
