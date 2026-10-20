namespace SkillBridge.Modules.Identity.Application.Users.DTOs.Responses;

public sealed record CurrentUserResponse(Guid Id, string Email, string FullName, string Role, bool IsActive);
