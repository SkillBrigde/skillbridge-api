using SkillBridge.Modules.Scheduling.Domain;

namespace SkillBridge.Modules.Scheduling.Application.DTOs.Responses;

public sealed record SchedulingSettingsResponse(
    Guid Id,
    Guid MentorId,
    int SessionDurationMinutes,
    int BufferTimeMinutes,
    int LeadTimeHours,
    int MaxDaysInAdvance,
    string Timezone)
{
    internal static SchedulingSettingsResponse From(SchedulingSettings settings) => new(
        settings.Id,
        settings.MentorId,
        settings.SessionDurationMinutes,
        settings.BufferTimeMinutes,
        settings.LeadTimeHours,
        settings.MaxDaysInAdvance,
        settings.Timezone);
}
