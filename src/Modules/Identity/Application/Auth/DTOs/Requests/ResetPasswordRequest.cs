namespace SkillBridge.Modules.Identity.Application.Auth.DTOs.Requests;

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
