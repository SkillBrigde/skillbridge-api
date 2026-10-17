namespace SkillBridge.BuildingBlocks.Events;

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset OccurredOnUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
}
