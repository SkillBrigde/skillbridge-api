using SkillBridge.Modules.Identity.Domain;

namespace SkillBridge.Modules.Identity.Application;

public sealed record RegisterRequest(string? Email, string? Password, string? FullName, string? Role);
public sealed record LoginRequest(string? Email, string? Password, bool RememberMe = false);
public sealed record RefreshRequest(string? RefreshToken, Guid SessionId);
public sealed record UpdateProfileRequest(string? FullName, string? AvatarUrl);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword, string? ConfirmNewPassword);
public sealed record SetStatusRequest(string? Status, string? LockReason);
public sealed record UserResponse(Guid Id, string Email, string FullName, string? AvatarUrl,
    string[] Roles, bool IsEmailVerified, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc)
{
    public static UserResponse From(User user) => new(user.Id, user.Email, user.FullName, user.AvatarUrl,
        user.Roles, user.IsEmailVerified, user.IsActive ? "Active" : "Locked", user.CreatedAtUtc, user.UpdatedAtUtc);
}
public sealed record AuthenticationResponse(string AccessToken, string RefreshToken, int ExpiresIn,
    Guid SessionId, UserResponse User);
public sealed record SessionResponse(Guid Id, string DeviceName, string IpAddress,
    DateTimeOffset LastActiveAtUtc, bool IsCurrentSession);
public sealed record UserPage(IReadOnlyList<UserResponse> Items, long TotalCount, int Page, int PageSize)
{
    public long TotalPages => (long)Math.Ceiling((double)TotalCount / PageSize);
}
