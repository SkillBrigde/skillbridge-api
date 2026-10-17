namespace SkillBridge.BuildingBlocks.RateLimiting;

public sealed record RateLimitDecision(
    bool IsAllowed,
    int RetryAfterSeconds);