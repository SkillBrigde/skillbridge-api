using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SkillBridge.Tests;

public sealed class ApiFactory(string? connectionString = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "test-only-key-32-bytes-minimum-do-not-use-in-production",
            ["Database:ApplyMigrations"] = "false",
            ["RateLimiting:AuthPermitLimit"] = connectionString is null ? "20" : "100",
            ["ConnectionStrings:Database"] = connectionString ??
                "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1"
        }));
    }
}
