using SkillBridge.Modules.Scheduling.Domain;

namespace SkillBridge.Modules.Scheduling.Application.DTOs.Responses;

public sealed record AvailabilityRuleResponse(
    Guid Id,
    Guid MentorId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive)
{
    internal static AvailabilityRuleResponse From(AvailabilityRule rule) => new(
        rule.Id, rule.MentorId, rule.DayOfWeek, rule.StartTime, rule.EndTime, rule.IsActive);
}
