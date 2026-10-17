namespace SkillBridge.BuildingBlocks.RateLimiting;

public interface IRateLimitStore
{
    Task<RateLimitDecision> TryAcquireAsync(
        string key,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}
