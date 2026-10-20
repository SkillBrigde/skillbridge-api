using System.Text.Json.Serialization;

namespace SkillBridge.Modules.Profiles.Application.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MentorExperienceRequest(
    string Company,
    string Position,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    string? Description,
    bool IsCurrent);
