using System.Diagnostics;
using SkillBridge.BuildingBlocks.RateLimiting;
using StackExchange.Redis;

namespace SkillBridge.IntegrationTests.RateLimiting;

public sealed class RedisRateLimitStoreTests : IAsyncLifetime
{
    private ConnectionMultiplexer? _connection;
    private IDatabase _database = null!;
    private RedisRateLimitStore _store = null!;

    private readonly string _key =
        $"skillbridge:test:rate-limit:{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("Redis__ConnectionString");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set Redis__ConnectionString before running Redis tests.");
        }

        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = true;
        options.ConnectRetry = 1;
        options.ConnectTimeout = 3000;
        options.AsyncTimeout = 3000;

        _connection = await ConnectionMultiplexer.ConnectAsync(options);
        _database = _connection.GetDatabase();
        _store = new RedisRateLimitStore(_connection);
    }

    public async Task DisposeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            await _database.KeyDeleteAsync(_key);
        }
        finally
        {
            _connection.Dispose();
        }
    }

    [RedisFact]
    public async Task Eleventh_request_is_rejected()
    {
        var window = TimeSpan.FromSeconds(60);

        for (var i = 0; i < 10; i++)
        {
            var decision = await _store.TryAcquireAsync(_key, 10, window);
            Assert.True(decision.IsAllowed);
        }

        var rejected = await _store.TryAcquireAsync(_key, 10, window);

        Assert.False(rejected.IsAllowed);
        Assert.InRange(rejected.RetryAfterSeconds, 1, 60);

        var ttl = await _database.KeyTimeToLiveAsync(_key);

        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromMilliseconds(1), window);
    }

    [RedisFact]
    public async Task Concurrent_requests_do_not_exceed_the_limit()
    {
        var attempts = Enumerable.Range(0, 50)
            .Select(_ => _store.TryAcquireAsync(
                _key, 10, TimeSpan.FromSeconds(60)));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(10, results.Count(result => result.IsAllowed));
        Assert.Equal(40, results.Count(result => !result.IsAllowed));
    }

    [RedisFact]
    public async Task Requests_are_allowed_again_after_expiry()
    {
        var window = TimeSpan.FromSeconds(3);

        var first = await _store.TryAcquireAsync(_key, 1, window);
        var second = await _store.TryAcquireAsync(_key, 1, window);

        Assert.True(first.IsAllowed);
        Assert.False(second.IsAllowed);

        var timer = Stopwatch.StartNew();

        while (await _database.KeyExistsAsync(_key)
            && timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(100);
        }

        Assert.False(await _database.KeyExistsAsync(_key));

        var afterExpiry = await _store.TryAcquireAsync(_key, 1, window);

        Assert.True(afterExpiry.IsAllowed);
    }
}

public sealed class RedisFactAttribute : FactAttribute
{
    public RedisFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_REDIS_TESTS") != "1")
        {
            Skip = "Set RUN_REDIS_TESTS=1 to run tests against Redis.";
        }
    }
}
