namespace SkillBridge.Modules.Identity.Application.Auth.Requests;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
