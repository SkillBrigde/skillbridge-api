namespace SkillBridge.Modules.Identity.Application.Users.DTOs.Responses;

public sealed record UserSummaryResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
