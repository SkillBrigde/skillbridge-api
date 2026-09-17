using SkillBridge.BuildingBlocks.Events;

namespace SkillBridge.Modules.Identity.Application;

public sealed record UserRegisteredIntegrationEvent(
    Guid EventId, DateTimeOffset OccurredOnUtc, Guid UserId, string Email, string FullName, string[] Roles) : IIntegrationEvent;
