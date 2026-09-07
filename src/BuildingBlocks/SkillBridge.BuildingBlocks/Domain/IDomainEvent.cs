namespace SkillBridge.BuildingBlocks.Domain;

public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
