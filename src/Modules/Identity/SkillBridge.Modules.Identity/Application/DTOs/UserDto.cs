namespace SkillBridge.Modules.Identity.Application.DTOs;

public sealed record UserDto(
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
