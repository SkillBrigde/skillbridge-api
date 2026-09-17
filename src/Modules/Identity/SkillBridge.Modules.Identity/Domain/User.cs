using System.Net.Mail;
using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Identity.Domain;

public sealed class User : AggregateRoot<Guid>
{
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string[] Roles { get; private set; } = [];
    public string? AvatarUrl { get; private set; }
    public string? PasswordHash { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public bool IsActive { get; private set; }
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEndUtc { get; private set; }
    public Guid SecurityStamp { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    private User() { }

    public static Result<User> Create(string? email, string? fullName, string? role, DateTimeOffset now)
    {
        if (!IsValidEmail(email))
            return Result.Failure<User>(Error.Validation("User.Email", "Địa chỉ email không hợp lệ."));
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 100)
            return Result.Failure<User>(Error.Validation("User.FullName", "Họ tên phải có từ 1 đến 100 ký tự."));
        if (role is not ("Mentor" or "Mentee"))
            return Result.Failure<User>(Error.Validation("User.Role", "Chỉ được đăng ký Mentor hoặc Mentee."));

        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = NormalizeEmail(email!),
            FullName = fullName.Trim(),
            Role = role,
            Roles = [role],
            IsActive = true,
            SecurityStamp = Guid.NewGuid(),
            CreatedAtUtc = now
        };
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Trim().Length <= 256 &&
        MailAddress.TryCreate(email.Trim(), out var address) && address.Address == email.Trim();

    public static Result ValidatePassword(string? password) =>
        password is { Length: >= 8 and <= 128 } && password.Any(char.IsUpper) &&
        password.Any(char.IsLower) && password.Any(char.IsDigit) &&
        password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
            ? Result.Success()
            : Result.Failure(Error.Validation("User.Password", "Mật khẩu cần 8–128 ký tự, chữ hoa, chữ thường, số và ký tự đặc biệt."));

    public bool IsLockedOut(DateTimeOffset now) => LockoutEndUtc > now;

    public void RecordFailedLogin(DateTimeOffset now)
    {
        if (LockoutEndUtc <= now)
        {
            AccessFailedCount = 0;
            LockoutEndUtc = null;
        }
        AccessFailedCount++;
        if (AccessFailedCount >= 5)
        {
            LockoutEndUtc = now.AddMinutes(15);
            AccessFailedCount = 0;
        }
    }

    public void RecordSuccessfulLogin()
    {
        AccessFailedCount = 0;
        LockoutEndUtc = null;
    }

    public void SetPasswordHash(string hash, DateTimeOffset now)
    {
        PasswordHash = hash;
        SecurityStamp = Guid.NewGuid();
        UpdatedAtUtc = now;
    }

    public Result UpdateProfile(string? fullName, string? avatarUrl, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 100)
            return Result.Failure(Error.Validation("User.FullName", "Họ tên phải có từ 1 đến 100 ký tự."));
        if (avatarUrl is not null && (avatarUrl.Length > 2048 ||
            !Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            return Result.Failure(Error.Validation("User.AvatarUrl", "Ảnh đại diện phải là URL HTTPS hợp lệ."));
        FullName = fullName.Trim();
        AvatarUrl = avatarUrl;
        UpdatedAtUtc = now;
        return Result.Success();
    }

    public Result SetStatus(string? status, DateTimeOffset now)
    {
        if (status is not ("Active" or "Locked"))
            return Result.Failure(Error.Validation("User.Status", "Trạng thái phải là Active hoặc Locked."));
        IsActive = status == "Active";
        SecurityStamp = Guid.NewGuid();
        UpdatedAtUtc = now;
        return Result.Success();
    }
}
