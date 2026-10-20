using System.Text.Json.Serialization;

namespace SkillBridge.Modules.Scheduling.Application.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateSchedulingSettingsRequest(
    int SessionDurationMinutes,
    int BufferTimeMinutes,
    int LeadTimeHours,
    int MaxDaysInAdvance,
    string Timezone);
