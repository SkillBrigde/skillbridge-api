using SkillBridge.BuildingBlocks.Domain;

namespace SkillBridge.Modules.Identity.Domain;

public sealed class User : AggregateRoot<Guid>
{
    public string Email { get; private set; } = default!;
    public string NormalizedEmail { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string SecurityStamp { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string? PhoneNumber { get; private set; }
    public string? AvatarUrl { get; private set; }
    public bool IsEmailConfirmed { get; private set; }
    public bool IsPhoneConfirmed { get; private set; }
    public bool TwoFactorEnabled { get; private set; }
    public DateTimeOffset? LockoutEndUtc { get; private set; }
    public bool LockoutEnabled { get; private set; } = true;
    public int AccessFailedCount { get; private set; }
    public string Role { get; private set; } = "Mentee";
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    private User() { }

    public static User Create(
        string email,
        string passwordHash,
        string fullName,
        string role = "Mentee",
        string? phoneNumber = null,
        string? avatarUrl = null)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();

        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = email.Trim().ToLowerInvariant(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            FullName = fullName.Trim(),
            PhoneNumber = phoneNumber?.Trim(),
            AvatarUrl = avatarUrl,
            Role = role,
            IsActive = true,
            IsEmailConfirmed = false,
            LockoutEnabled = true,
            AccessFailedCount = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public void UpdateProfile(string fullName, string? phoneNumber, string? avatarUrl)
    {
        FullName = fullName.Trim();
        PhoneNumber = phoneNumber?.Trim();
        AvatarUrl = avatarUrl;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        SecurityStamp = Guid.NewGuid().ToString("N");
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void RecordFailedLogin(int maxFailedAttempts = 5, TimeSpan? lockoutDuration = null)
    {
        if (!LockoutEnabled) return;

        if (LockoutEndUtc.HasValue && !IsLockedOut)
        {
            ResetFailedLogin();
        }

        AccessFailedCount++;
        if (AccessFailedCount >= maxFailedAttempts)
        {
            var duration = lockoutDuration ?? TimeSpan.FromMinutes(15);
            LockoutEndUtc = DateTimeOffset.UtcNow.Add(duration);
        }
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ResetFailedLogin()
    {
        AccessFailedCount = 0;
        LockoutEndUtc = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public bool IsLockedOut => LockoutEndUtc.HasValue && LockoutEndUtc.Value > DateTimeOffset.UtcNow;

    public void SetStatus(bool isActive)
    {
        if (IsActive && !isActive)
        {
            SecurityStamp = Guid.NewGuid().ToString("N");
        }
        IsActive = isActive;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ConfirmEmail()
    {
        IsEmailConfirmed = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
