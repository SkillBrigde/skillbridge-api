using SkillBridge.BuildingBlocks.Domain;

namespace SkillBridge.Modules.Identity.Domain;

public sealed class RefreshToken : AggregateRoot<Guid>
{
    public Guid SessionId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid sessionId, string hash, DateTimeOffset expiresAt) => new()
    {
        Id = Guid.CreateVersion7(),
        SessionId = sessionId,
        TokenHash = hash,
        ExpiresAtUtc = expiresAt
    };

    public void Revoke(Guid? replacementId = null)
    {
        IsRevoked = true;
        ReplacedByTokenId = replacementId;
    }
}
