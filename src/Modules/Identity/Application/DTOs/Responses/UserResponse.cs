namespace SkillBridge.Modules.Identity.Application.DTOs.Responses;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    string? PhoneNumber,
    string? AvatarUrl,
    bool IsEmailConfirmed,
    bool IsActive,
    DateTimeOffset CreatedAtUtc
);
