namespace SkillBridge.Modules.Profiles.Application.DTOs.Requests;

public sealed record UpdateMenteeProfileRequest(
    string CareerGoal,
    string CurrentLevel,
    string Timezone,
    decimal BudgetMin,
    decimal BudgetMax,
    string Currency,
    string? AvatarUrl = null);
