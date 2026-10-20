namespace SkillBridge.Modules.Identity.Application.Auth.DTOs.Responses;

public sealed record TokenResponse(string AccessToken, int ExpiresIn);
