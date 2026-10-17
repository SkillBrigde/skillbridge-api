namespace SkillBridge.Modules.Identity.Application.DTOs;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string AccessToken,
    string RefreshToken,
    int ExpiresIn
);
