using MediatR;

namespace SkillBridge.BuildingBlocks.Events;

public interface IIntegrationEvent : INotification
{
    Guid EventId => Guid.NewGuid();
    DateTimeOffset OccurredOnUtc => DateTimeOffset.UtcNow;
    string EventType => GetType().Name;
}
