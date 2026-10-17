using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkillBridge.Api.Security;
using SkillBridge.BuildingBlocks.Security;

namespace SkillBridge.Tests.Security;

public sealed class SecurityRegistrationTests
{
    private const string SigningKey = "FixtureOnlySigningKeyForSkillBridgeTestsWithAtLeastSixtyFourCharacters!";
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000017");

    [Fact]
    public void Security_services_work_without_HTTP_authentication()
    {
        var services = new ServiceCollection();
        services.AddSecurityServices(Configuration());
        using var provider = services.BuildServiceProvider();
        var hasher = provider.GetRequiredService<IPasswordHasher>();
        var hash = hasher.HashPassword("FixturePassword1!");

        Assert.True(hasher.VerifyPassword("FixturePassword1!", hash));
        Assert.False(hasher.VerifyPassword("WrongPassword1!", hash));
        Assert.Same(hasher, provider.GetRequiredService<IPasswordHasher>());
        Assert.Same(provider.GetRequiredService<IJwtTokenGenerator>(), provider.GetRequiredService<IJwtTokenGenerator>());
        Assert.Null(provider.GetService<IAuthenticationSchemeProvider>());
        var settings = provider.GetRequiredService<JwtSettings>();
        Assert.Equal("FixtureIssuer", settings.Issuer);
        Assert.Equal("FixtureAudience", settings.Audience);
        Assert.Equal(23, settings.AccessTokenExpirationMinutes);
        Assert.Equal(9, settings.RefreshTokenExpirationDays);
    }

    [Fact]
    public void Registered_generator_issues_signed_tokens_and_random_refresh_tokens()
    {
        var services = new ServiceCollection().AddSecurityServices(Configuration());
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IJwtTokenGenerator>();
        var token = generator.GenerateAccessToken(UserId, "fixture@example.test", "Fixture User", ["Mentor"]);
        var principal = generator.GetPrincipalFromExpiredToken(token)!;
        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(UserId.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("fixture@example.test", principal.FindFirstValue(ClaimTypes.Email));
        Assert.True(principal.IsInRole("Mentor"));
        Assert.Equal("FixtureIssuer", decoded.Issuer);
        Assert.Contains("FixtureAudience", decoded.Audiences);
        Assert.InRange(decoded.ValidTo, DateTime.UtcNow.AddMinutes(22), DateTime.UtcNow.AddMinutes(24));
        var refresh = generator.GenerateRefreshToken();
        Assert.Equal(64, Convert.FromBase64String(refresh).Length);
        Assert.NotEqual(refresh, generator.GenerateRefreshToken());
    }

    [Theory]
    [InlineData("valid", 200)]
    [InlineData("anonymous", 401)]
    [InlineData("wrong issuer", 401)]
    [InlineData("wrong audience", 401)]
    [InlineData("wrong signature", 401)]
    [InlineData("expired", 401)]
    public async Task API_authentication_accepts_issued_tokens_and_rejects_invalid_tokens(string scenario, int expectedStatus)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddSecurityServices(Configuration());
        builder.Services.AddApiAuthentication();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/me", (ICurrentUser user) => new
        {
            user.UserId,
            user.Email,
            user.Role,
            user.IsAuthenticated,
            IsMentor = user.HasRole("Mentor")
        }).RequireAuthorization();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var settings = new JwtSettings
        {
            SecretKey = scenario == "wrong signature" ? SigningKey + "different" : SigningKey,
            Issuer = scenario == "wrong issuer" ? "WrongIssuer" : "FixtureIssuer",
            Audience = scenario == "wrong audience" ? "WrongAudience" : "FixtureAudience",
            AccessTokenExpirationMinutes = scenario == "expired" ? -1 : 23
        };
        if (scenario != "anonymous")
        {
            var generator = scenario == "valid" ? app.Services.GetRequiredService<IJwtTokenGenerator>()
                : new JwtTokenGenerator(settings);
            var token = generator.GenerateAccessToken(UserId, "fixture@example.test", "Fixture User", ["Mentor"]);
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }

        using var response = await client.GetAsync("/me");

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        if (expectedStatus == 200)
        {
            var body = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.Equal(UserId, body["userId"]!.GetValue<Guid>());
            Assert.Equal("fixture@example.test", body["email"]!.GetValue<string>());
            Assert.Equal("Mentor", body["role"]!.GetValue<string>());
            Assert.True(body["isAuthenticated"]!.GetValue<bool>());
            Assert.True(body["isMentor"]!.GetValue<bool>());
        }
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = SigningKey,
            ["Jwt:Issuer"] = "FixtureIssuer",
            ["Jwt:Audience"] = "FixtureAudience",
            ["Jwt:AccessTokenExpirationMinutes"] = "23",
            ["Jwt:RefreshTokenExpirationDays"] = "9"
        }).Build();
}
