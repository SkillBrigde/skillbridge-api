namespace SkillBridge.Modules.Identity.Application.Auth.Responses;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string AccessToken,
    string RefreshToken,
    int ExpiresIn
);
