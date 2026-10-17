namespace SkillBridge.Modules.Identity.Application.Auth.DTOs.Requests;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
