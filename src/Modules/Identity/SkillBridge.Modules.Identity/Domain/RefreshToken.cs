using SkillBridge.BuildingBlocks.Domain;

namespace SkillBridge.Modules.Identity.Domain;

public sealed class RefreshToken : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = default!;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? ReplacedByToken { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string token, DateTimeOffset expiresAtUtc, string? createdByIp = null)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            CreatedByIp = createdByIp,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc;
    public bool IsRevoked => RevokedAtUtc != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    public void Revoke(string? replacedByToken = null)
    {
        RevokedAtUtc = DateTimeOffset.UtcNow;
        ReplacedByToken = replacedByToken;
    }
}
