namespace SkillBridge.Modules.Identity.Application.Auth;

public sealed record AuthenticationResult(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string AccessToken,
    string RefreshToken,
    int ExpiresIn
);
