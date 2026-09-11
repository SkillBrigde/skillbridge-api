using MediatR;

namespace SkillBridge.BuildingBlocks.Domain;

public interface IDomainEvent : INotification
{
    Guid EventId => Guid.NewGuid();
    DateTimeOffset OccurredOnUtc => DateTimeOffset.UtcNow;
}
