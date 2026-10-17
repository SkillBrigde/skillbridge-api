namespace SkillBridge.Modules.Profiles.Application.DTOs.Requests;

public sealed record UpdateMentorProfileRequest(
    string Headline,
    string Bio,
    decimal HourlyRate,
    string Currency,
    string Timezone,
    bool IsAcceptingMentees,
    string? AvatarUrl = null);
