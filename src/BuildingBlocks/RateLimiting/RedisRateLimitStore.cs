using StackExchange.Redis;

namespace SkillBridge.BuildingBlocks.RateLimiting;

public sealed class RedisRateLimitStore : IRateLimitStore
{
    private readonly IDatabase _database;

    private const string AcquireScript = """
        local count = redis.call('INCR', KEYS[1])
        local ttl = redis.call('PTTL', KEYS[1])

        if ttl < 0 then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
            ttl = tonumber(ARGV[2])
        end

        if count <= tonumber(ARGV[1]) then
            return 0
        end

        return math.max(ttl, 1)
        """;

    public RedisRateLimitStore(IConnectionMultiplexer connection)
    {
        _database = connection.GetDatabase();
    }

    public async Task<RateLimitDecision> TryAcquireAsync(
        string key,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permitLimit);

        if (window < TimeSpan.FromMilliseconds(1)
            || window > TimeSpan.FromSeconds(int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var windowMilliseconds =
            (long)Math.Ceiling(window.TotalMilliseconds);

        var result = await _database.ScriptEvaluateAsync(
            AcquireScript,
            keys: new RedisKey[] { key },
            values: new RedisValue[] { permitLimit, windowMilliseconds })
            .WaitAsync(cancellationToken);

        var retryAfterMilliseconds = (long)result;

        if (retryAfterMilliseconds == 0)
        {
            return new RateLimitDecision(
                IsAllowed: true,
                RetryAfterSeconds: 0);
        }

        var retryAfterSeconds = checked(
            (int)Math.Ceiling(retryAfterMilliseconds / 1000d));

        return new RateLimitDecision(
            IsAllowed: false,
            RetryAfterSeconds: retryAfterSeconds);
    }
}