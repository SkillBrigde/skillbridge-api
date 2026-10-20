using System.Text.Json.Serialization;

namespace SkillBridge.Modules.Scheduling.Application.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateAvailabilityRuleRequest(
    [property: JsonRequired] int DayOfWeek,
    [property: JsonRequired] TimeOnly StartTime,
    [property: JsonRequired] TimeOnly EndTime,
    [property: JsonRequired] bool IsActive);
