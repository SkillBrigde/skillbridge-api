using SkillBridge.BuildingBlocks.Domain;

namespace SkillBridge.Modules.Identity.Domain;

public sealed class ExternalLogin : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = default!; // 'Google', 'GitHub', 'LinkedIn'
    public string ProviderKey { get; private set; } = default!;
    public string? ProviderDisplayName { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private ExternalLogin() { }

    public static ExternalLogin Create(Guid userId, string provider, string providerKey, string? displayName = null)
    {
        return new ExternalLogin
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Provider = provider,
            ProviderKey = providerKey,
            ProviderDisplayName = displayName,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
