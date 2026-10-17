using System.Net;
using System.Net.Http.Json;
using SkillBridge.BuildingBlocks.RateLimiting;

namespace SkillBridge.IntegrationTests.Identity;

public sealed class LoginRateLimitTests(IdentityApiFixture api)
    : IClassFixture<IdentityApiFixture>
{
    [Theory]
    [InlineData(false, HttpStatusCode.TooManyRequests)]
    [InlineData(true, HttpStatusCode.ServiceUnavailable)]
    public async Task Login_does_not_run_when_rate_limit_rejects_or_fails(
        bool unavailable,
        HttpStatusCode expectedStatus)
    {
        var previousDecision = api.RateLimiter.Decision;
        var previousFailure = api.RateLimiter.Failure;

        try
        {
            api.RateLimiter.Decision = new RateLimitDecision(
                IsAllowed: false,
                RetryAfterSeconds: 30);

            api.RateLimiter.Failure = unavailable
                ? new TimeoutException("Fixture timeout.")
                : null;

            api.Sender.LastRequest = null;

            using var response = await api.Client.PostAsJsonAsync(
                "/api/v1/identity/auth/login",
                new
                {
                    email = "fixture@example.test",
                    password = "Fixture1!"
                });

            Assert.Equal(expectedStatus, response.StatusCode);
            Assert.Null(api.Sender.LastRequest);
            Assert.False(response.Headers.Contains("Set-Cookie"));

            if (unavailable)
            {
                Assert.Null(response.Headers.RetryAfter);
            }
            else
            {
                Assert.Equal(
                    TimeSpan.FromSeconds(30),
                    response.Headers.RetryAfter?.Delta);
            }
        }
        finally
        {
            api.RateLimiter.Decision = previousDecision;
            api.RateLimiter.Failure = previousFailure;
        }
    }
}