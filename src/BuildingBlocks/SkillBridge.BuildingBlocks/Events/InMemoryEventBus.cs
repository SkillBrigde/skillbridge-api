using MediatR;
using Microsoft.Extensions.Logging;

namespace SkillBridge.BuildingBlocks.Events;

public sealed class InMemoryEventBus : IEventBus
{
    private readonly IPublisher _publisher;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(IPublisher publisher, ILogger<InMemoryEventBus> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        _logger.LogInformation("Phát Integration Event: {EventType} ({EventId})", @event.EventType, @event.EventId);
        await _publisher.Publish(@event, cancellationToken);
    }
}
