using SkillBridge.BuildingBlocks.Domain;

namespace SkillBridge.Modules.Identity.Domain;

public sealed class UserSession : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string UserAgent { get; private set; } = string.Empty;
    public string IpAddress { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastActiveAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    private UserSession() { }

    public static UserSession Create(Guid userId, string userAgent, string ipAddress, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        UserAgent = userAgent[..Math.Min(userAgent.Length, 512)],
        IpAddress = ipAddress[..Math.Min(ipAddress.Length, 64)],
        IsActive = true,
        CreatedAtUtc = now,
        LastActiveAtUtc = now,
        ExpiresAtUtc = now.AddDays(7)
    };

    public bool IsValid(DateTimeOffset now) => IsActive && ExpiresAtUtc > now;
    public void Touch(DateTimeOffset now) => LastActiveAtUtc = now;
    public void Revoke() => IsActive = false;
}
