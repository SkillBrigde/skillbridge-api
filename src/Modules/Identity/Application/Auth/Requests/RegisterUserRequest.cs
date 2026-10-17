namespace SkillBridge.Modules.Identity.Application.Auth.Requests;

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string FullName,
    string Role = "Mentee",
    string? PhoneNumber = null
);
