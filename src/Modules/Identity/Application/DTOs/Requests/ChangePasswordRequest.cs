namespace SkillBridge.Modules.Identity.Application.DTOs.Requests;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
