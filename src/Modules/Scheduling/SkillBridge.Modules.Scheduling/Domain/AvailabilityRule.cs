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
}
