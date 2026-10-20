namespace SkillBridge.Modules.Profiles.Application.DTOs.Responses;

public sealed record MenteeProfileResponse(
    Guid Id,
    string CareerGoal,
    string CurrentLevel,
    string Timezone,
    decimal BudgetMin,
    decimal BudgetMax,
    string Currency,
    string? AvatarUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
