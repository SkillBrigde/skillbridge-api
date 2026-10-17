using SkillBridge.BuildingBlocks.Domain;

namespace SkillBridge.Modules.Identity.Domain;

public sealed class PasswordResetToken : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public string SecurityStamp { get; private set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    private PasswordResetToken() { }

    public static PasswordResetToken Create(Guid userId, string token, string securityStamp, DateTimeOffset now)
    {
        var reset = new PasswordResetToken { Id = Guid.CreateVersion7(), UserId = userId };
        reset.Replace(token, securityStamp, now);
        return reset;
    }

    public void Replace(string token, string securityStamp, DateTimeOffset now)
    {
        TokenHash = RefreshToken.Hash(token);
        SecurityStamp = securityStamp;
        CreatedAtUtc = now;
        ExpiresAtUtc = now.AddMinutes(30);
        ConsumedAtUtc = null;
    }

    public bool CanUse(string tokenHash, string securityStamp, DateTimeOffset now) =>
        ConsumedAtUtc is null && ExpiresAtUtc > now && TokenHash == tokenHash && SecurityStamp == securityStamp;

    public void Consume(DateTimeOffset now) => ConsumedAtUtc = now;
}
