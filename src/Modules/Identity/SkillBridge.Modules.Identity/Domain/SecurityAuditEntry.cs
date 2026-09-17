using SkillBridge.BuildingBlocks.Domain;

namespace SkillBridge.Modules.Identity.Domain;

public sealed class SecurityAuditEntry : AggregateRoot<Guid>
{
    public Guid ActorId { get; private set; }
    public Guid SubjectId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private SecurityAuditEntry() { }

    public static SecurityAuditEntry Create(Guid actorId, Guid subjectId, string action, string? reason, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        ActorId = actorId,
        SubjectId = subjectId,
        Action = action,
        Reason = reason,
        CreatedAtUtc = now
    };
}
