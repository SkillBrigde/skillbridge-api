using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SkillBridge.Tests;

public sealed class ApiBoundaryTests
{
    [Theory]
    [InlineData("/api/v1/identity/users/me")]
    [InlineData("/api/v1/identity/users")]
    [InlineData("/api/v1/sessions")]
    public async Task Private_endpoints_require_authentication(string path)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Invalid_registration_returns_problem_details_with_correlation()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "test-correlation");
        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/register",
            new { email = "a@example.com", fullName = "Name", role = "Admin", password = "Valid123!" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("User.Role", problem.GetProperty("title").GetString());
        Assert.Equal("test-correlation", problem.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Rate_limit_blocks_repeated_auth_requests()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < 20; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/identity/auth/login",
                new { email = "", password = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/v1/identity/auth/login",
            new { email = "", password = "" })).StatusCode);
    }

    [Fact]
    public async Task Liveness_succeeds_but_readiness_fails_when_database_is_unavailable()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }
}
