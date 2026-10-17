namespace SkillBridge.Modules.Identity.Application.Users.DTOs.Responses;

public sealed record UserDetailsResponse(Guid Id, string Email, string FullName, string Role, bool IsActive,
    bool TwoFactorEnabled, DateTimeOffset? LockoutEndUtc, int AccessFailedCount,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
