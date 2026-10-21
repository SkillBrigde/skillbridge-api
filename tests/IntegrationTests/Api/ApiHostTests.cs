using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkillBridge.Api.Endpoints;
using SkillBridge.Api.Extensions;
using SkillBridge.Api.Modules;

namespace SkillBridge.IntegrationTests.Api;

public sealed class ApiHostTests
{
    private static readonly string[] ModuleNames =
    [
        "Identity", "Profiles", "Catalog", "Scheduling", "Booking",
        "Payments", "Learning", "Messaging", "Reviews", "Recommendations"
    ];

    [Fact]
    public async Task Root_reports_all_registered_modules_in_the_existing_order()
    {
        await using var app = await StartAppAsync(environmentName: Environments.Development);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("SkillBridge API", body.RootElement.GetProperty("service").GetString());
        Assert.Equal(10, body.RootElement.GetProperty("totalModules").GetInt32());
        Assert.Equal(ModuleNames, body.RootElement.GetProperty("activeModules")
            .EnumerateArray().Select(module => module.GetString()));
        Assert.Equal("/scalar/v1", body.RootElement.GetProperty("scalarDocs").GetString());
        Assert.Equal("/openapi/v1.json", body.RootElement.GetProperty("openApiSpec").GetString());
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task Production_root_does_not_expose_module_metadata()
    {
        await using var app = await StartAppAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("identity", "Identity")]
    [InlineData("profiles", "Profiles")]
    [InlineData("catalog", "Catalog")]
    [InlineData("scheduling", "Scheduling")]
    [InlineData("booking", "Booking")]
    [InlineData("payments", "Payments")]
    [InlineData("learning", "Learning")]
    [InlineData("messaging", "Messaging")]
    [InlineData("reviews", "Reviews")]
    [InlineData("recommendations", "Recommendations")]
    public async Task Every_module_keeps_its_probe_route(string prefix, string expectedName)
    {
        await using var app = await StartAppAsync(environmentName: Environments.Development);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync($"/api/v1/{prefix}/_module");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedName, body.RootElement.GetProperty("name").GetString());
        Assert.Equal(prefix, body.RootElement.GetProperty("routePrefix").GetString());
        Assert.Equal("available", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Production_does_not_publish_module_probe_routes()
    {
        await using var app = await StartAppAsync();
        using var client = app.GetTestClient();

        foreach (var prefix in ModuleNames.Select(name => name.ToLowerInvariant()))
        {
            using var response = await client.GetAsync($"/api/v1/{prefix}/_module");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("/health/live", HttpStatusCode.OK, "Healthy")]
    [InlineData("/health/ready", HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    public async Task Readiness_includes_database_health_while_liveness_remains_independent(
        string path, HttpStatusCode expectedStatus, string expectedBody)
    {
        await using var app = await StartAppAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(expectedBody, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/v1/identity/users/me")]
    [InlineData("/api/v1/identity/users/")]
    public async Task Host_pipeline_challenges_anonymous_requests_to_protected_endpoints(string path)
    {
        await using var app = await StartAppAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Theory]
    [InlineData(true, "https://client.example.test", "https://client.example.test", true)]
    [InlineData(true, "https://other.example.test", null, false)]
    [InlineData(false, "https://client.example.test", "*", false)]
    public async Task CORS_keeps_configured_origins_and_the_existing_wildcard_fallback(
        bool configuredOrigins, string origin, string? expectedOrigin, bool expectCredentials)
    {
        await using var app = await StartAppAsync(configuredOrigins);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (expectedOrigin == null)
        {
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
        else
        {
            Assert.Equal(expectedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        }

        Assert.Equal(expectCredentials, response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Trusted_proxy_uses_only_the_last_forwarded_client_address()
    {
        await using var app = await StartAppAsync(knownProxy: "172.18.0.1");
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_test/proxy");
        request.Headers.Add("X-Forwarded-For", "203.0.113.1, 198.51.100.42");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("198.51.100.42", body.RootElement.GetProperty("ip").GetString());
        Assert.Equal("https", body.RootElement.GetProperty("scheme").GetString());
    }

    [Fact]
    public async Task Untrusted_proxy_cannot_replace_the_client_address()
    {
        await using var app = await StartAppAsync(knownProxy: "172.18.0.2");
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_test/proxy");
        request.Headers.Add("X-Forwarded-For", "198.51.100.42");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("172.18.0.1", body.RootElement.GetProperty("ip").GetString());
        Assert.Equal("http", body.RootElement.GetProperty("scheme").GetString());
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task Production_does_not_publish_API_documentation(string path)
    {
        await using var app = await StartAppAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<WebApplication> StartAppAsync(
        bool configuredOrigins = false, string? environmentName = null, string? knownProxy = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            EnvironmentName = environmentName ?? Environments.Production
        });
        builder.Configuration.Sources.Clear();
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "FixtureOnlyHostSigningKeyWithAtLeastSixtyFourCharactersForTests12345!",
            ["ConnectionStrings:Database"] = "Host=127.0.0.1;Database=fixture;Username=fixture;Password=fixture",
            ["Redis:ConnectionString"] = "127.0.0.1:6379"
        };
        if (configuredOrigins)
            settings["Cors:AllowedOrigins:0"] = "https://client.example.test";
        if (knownProxy is not null)
            settings["ReverseProxy:KnownProxy"] = knownProxy;
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();

        var modules = ModuleRegistry.Create();
        builder.Services.AddApiServices(builder.Configuration, modules);
        // Exercise the real host registration and route predicates without requiring a database.
        builder.Services.PostConfigure<HealthCheckServiceOptions>(options =>
        {
            var database = options.Registrations.Single(check => check.Name == "database");
            options.Registrations.Remove(database);
            options.Registrations.Add(new HealthCheckRegistration("database",
                _ => new UnavailableDatabase(), HealthStatus.Unhealthy, database.Tags));
        });
        var app = builder.Build();
        if (knownProxy is not null)
            app.Use((context, next) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.1");
                return next();
            });
        app.UseApiPipeline();
        app.MapGet("/_test/proxy", (HttpContext context) => Results.Ok(new
        {
            ip = context.Connection.RemoteIpAddress?.ToString(),
            scheme = context.Request.Scheme
        }));
        app.MapApiDocumentation();
        app.MapHostEndpoints(modules);
        app.MapModuleEndpoints(modules);
        // Production startup does not initialize modules or connect to their databases.
        await app.StartAsync();
        return app;
    }

    private sealed class UnavailableDatabase : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Unhealthy("Fixture database unavailable."));
    }
}
