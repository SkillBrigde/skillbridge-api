using SkillBridge.Modules.Scheduling.Domain;

namespace SkillBridge.Modules.Scheduling.Application;

public sealed record AvailabilityRuleDto(
    Guid Id,
    Guid MentorId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive)
{
    internal static AvailabilityRuleDto From(AvailabilityRule rule) => new(
        rule.Id, rule.MentorId, rule.DayOfWeek, rule.StartTime, rule.EndTime, rule.IsActive);
}
