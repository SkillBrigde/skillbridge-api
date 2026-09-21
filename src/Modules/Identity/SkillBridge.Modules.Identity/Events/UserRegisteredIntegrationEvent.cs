using SkillBridge.BuildingBlocks.Events;

namespace SkillBridge.Modules.Identity.Events;

public sealed record UserRegisteredIntegrationEvent(
    Guid UserId,
    string Email,
    string FullName,
    string Role
) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredOnUtc { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(UserRegisteredIntegrationEvent);
}
