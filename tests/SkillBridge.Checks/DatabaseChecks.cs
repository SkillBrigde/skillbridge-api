using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using SkillBridge.Api.Common;
using SkillBridge.Api.Middleware;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Extensions;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Catalog;
using SkillBridge.Modules.Catalog.Infrastructure;
using SkillBridge.Modules.Identity;
using SkillBridge.Modules.Identity.Application.Commands.Login;
using SkillBridge.Modules.Identity.Application.Commands.RefreshToken;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Checks;

internal static class DatabaseChecks
{
    public static async Task RunAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("SKILLBRIDGE_TEST_DATABASE");
        Program.Check(!string.IsNullOrWhiteSpace(connectionString), "--database requires SKILLBRIDGE_TEST_DATABASE (a PostgreSQL connection with CREATEDB permission).");
        var database = $"skillbridge_checks_{Guid.NewGuid():N}";
        var testConnection = new NpgsqlConnectionStringBuilder(connectionString) { Database = database, Pooling = false };
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await RunHttpChecksAsync(testConnection.ConnectionString);
        }
        finally
        {
            // Only the random database created by this invocation can be removed.
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
        Console.WriteLine("PASS: PostgreSQL migrations, HTTP contracts, Admin authorization, token rotation/revocation and Catalog concurrency.");
    }

    private static async Task RunHttpChecksAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = connectionString,
            ["Jwt:SecretKey"] = "Test-only-signing-key-created-for-local-checks-never-use-in-production-2026",
            ["Jwt:Issuer"] = "SkillBridge.Checks",
            ["Jwt:Audience"] = "SkillBridge.Checks"
        });
        builder.Logging.ClearProviders();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                $"{context.Connection.RemoteIpAddress}:{context.GetEndpoint()?.DisplayName}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.Services.AddBuildingBlocks(builder.Configuration, typeof(IdentityModule).Assembly, typeof(CatalogModule).Assembly);
        builder.Services.AddJwtSecurity(builder.Configuration);
        var identity = new IdentityModule();
        var catalog = new CatalogModule();
        identity.AddServices(builder.Services, builder.Configuration);
        catalog.AddServices(builder.Services, builder.Configuration);
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        identity.MapEndpoints(app);
        catalog.MapEndpoints(app);
        app.Urls.Add("http://127.0.0.1:0");

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var taxonomy = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await users.GetService<IMigrator>().MigrateAsync("20260911052416_Initial_Identity");
            var legacyOne = Guid.NewGuid();
            var legacyTwo = Guid.NewGuid();
            await users.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO identity.users ("Id", "Email", "FullName", "Role", "IsActive", "CreatedAtUtc")
                VALUES ({legacyOne}, {"Legacy.One@checks.test"}, {"Legacy One"}, {"Mentee"}, {true}, {DateTimeOffset.UtcNow}),
                       ({legacyTwo}, {"Legacy.Two@checks.test"}, {"Legacy Two"}, {"Mentor"}, {true}, {DateTimeOffset.UtcNow})
                """);
            await users.Database.MigrateAsync();
            var upgraded = await users.Users.AsNoTracking().Where(user => user.Id == legacyOne || user.Id == legacyTwo).ToListAsync();
            Program.Check(upgraded.Count == 2 && upgraded.Select(user => user.NormalizedEmail).Distinct().Count() == 2 &&
                upgraded.All(user => user.NormalizedEmail == user.Email.Trim().ToUpperInvariant()), "Legacy email migration did not backfill unique normalized values.");
            Program.Check(upgraded.All(user => string.IsNullOrEmpty(user.PasswordHash) && !user.IsActive),
                "Legacy users without credentials must not receive fabricated passwords or active accounts.");
            await taxonomy.Database.MigrateAsync();
            Program.Check(!(await users.Database.GetPendingMigrationsAsync()).Any() && !(await taxonomy.Database.GetPendingMigrationsAsync()).Any(),
                "PostgreSQL migrations remain pending.");
            users.Users.Add(User.Create("admin@checks.test", scope.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword("Password123!"), "Admin", "Admin"));
            await users.SaveChangesAsync();
        }

        await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(app.Urls.Single()) };
            using var invalid = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/register", 400,
                new { email = "not-an-email", password = "weak", fullName = "", role = "Admin" });
            Program.Check((await JsonAsync(invalid)).TryGetProperty("errors", out _), "Validation response must include field errors.");
            using var registered = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/register", 201,
                new { email = "mentee@checks.test", password = "Password123!", fullName = "Mentee", role = "Mentee" });
            var userId = (await JsonAsync(registered)).GetProperty("id").GetGuid();
            using var duplicate = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/register", 409,
                new { email = "MENTEE@checks.test", password = "Password123!", fullName = "Mentee", role = "Mentee" });
            await using (var scope = app.Services.CreateAsyncScope())
                Program.Check(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<OutboxMessage>().CountAsync() == 1,
                    "Registration must persist exactly one outbox event; duplicate registration must not emit another.");
            using var badLogin = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/login", 401,
                new { email = "absent@checks.test", password = "Password123!" });
            using var legacyLogin = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/login", 401,
                new { email = "Legacy.One@checks.test", password = "Password123!" });
            using var oversizedPassword = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/register", 400,
                new { email = "oversized@checks.test", password = "Ab1!" + new string('đ', 35), fullName = "Oversized", role = "Mentee" });
            var mentee = await LoginAsync(client, "mentee@checks.test");
            var admin = await LoginAsync(client, "admin@checks.test");
            using var adminMe = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users/me", 200, bearer: admin.AccessToken);
            var adminId = (await JsonAsync(adminMe)).GetProperty("id").GetGuid();
            using var selfDisable = await SendAsync(client, HttpMethod.Put, $"/api/v1/identity/users/{adminId}/status", 403,
                new { isActive = false }, admin.AccessToken);
            using var foreignLogout = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/logout", 204,
                bearer: mentee.AccessToken, cookie: admin.Cookie);
            using var unknownLogout = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/logout", 204,
                bearer: mentee.AccessToken, cookie: "refreshToken=unknown");

            using var anonymousUsers = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users", 401);
            using var forbiddenUsers = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users", 403, bearer: mentee.AccessToken);
            using var forbiddenStatus = await SendAsync(client, HttpMethod.Put, $"/api/v1/identity/users/{userId}/status", 403,
                new { isActive = false }, mentee.AccessToken);
            using var adminUsers = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users?page=1&pageSize=10", 200, bearer: admin.AccessToken);
            Program.Check((await JsonAsync(adminUsers)).GetProperty("page").GetInt32() == 1, "Users API did not serialize page.");
            using var forbiddenDetail = await SendAsync(client, HttpMethod.Get, $"/api/v1/identity/users/{userId}", 403, bearer: mentee.AccessToken);
            using var adminDetail = await SendAsync(client, HttpMethod.Get, $"/api/v1/identity/users/{userId}", 200, bearer: admin.AccessToken);
            Program.Check((await JsonAsync(adminDetail)).GetProperty("id").GetGuid() == userId, "Admin user details returned another user.");
            using var missingStatus = await SendAsync(client, HttpMethod.Put, $"/api/v1/identity/users/{userId}/status", 400, new { }, admin.AccessToken);
            using var nullStatus = await SendAsync(client, HttpMethod.Put, $"/api/v1/identity/users/{userId}/status", 400, new { isActive = (bool?)null }, admin.AccessToken);
            using var updateMe = await SendAsync(client, HttpMethod.Put, "/api/v1/identity/users/me", 204, new { fullName = "Updated Mentee" }, mentee.AccessToken);
            using var me = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users/me", 200, bearer: mentee.AccessToken);
            Program.Check((await JsonAsync(me)).GetProperty("fullName").GetString() == "Updated Mentee", "Updating current user did not persist fullName.");

            await CheckCatalogAsync(client, admin.AccessToken, mentee.AccessToken);
            var concurrentAccess = await CheckConcurrentRefreshAsync(app.Services, mentee.RefreshToken, userId);
            using var originalRevoked = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users/me", 401, bearer: mentee.AccessToken);
            using var winnerRevoked = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users/me", 401, bearer: concurrentAccess);
            await CheckFailedLoginLockoutAsync(app.Services);
            using var replay = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/refresh", 401, cookie: mentee.Cookie);
            await using (var scope = app.Services.CreateAsyncScope())
                Program.Check(await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().OutboxMessages.CountAsync() == 1,
                    "Skill creation must persist exactly one outbox event; rejected writes must not emit another.");

            var session = await LoginAsync(client, "mentee@checks.test");
            using var refreshed = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/refresh", 200, cookie: session.Cookie);
            var refreshCookie = refreshed.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("refreshToken=", StringComparison.Ordinal)).Split(';')[0];
            var refreshedAccess = (await JsonAsync(refreshed)).GetProperty("accessToken").GetString()!;
            using var reused = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/refresh", 401, cookie: session.Cookie);
            using var reusedAccess = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users/me", 401, bearer: refreshedAccess);
            using var reusedRefresh = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/refresh", 401, cookie: refreshCookie);
            var passwordSession = await LoginAsync(client, "mentee@checks.test");
            using var passwordChanged = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/change-password", 204,
                new { currentPassword = "Password123!", newPassword = "NewPassword123!" }, passwordSession.AccessToken);
            using var staleAccess = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users/me", 401, bearer: passwordSession.AccessToken);
            using var staleRefresh = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/refresh", 401, cookie: passwordSession.Cookie);

            var newSession = await LoginAsync(client, "mentee@checks.test", "NewPassword123!");
            using var disabled = await SendAsync(client, HttpMethod.Put, $"/api/v1/identity/users/{userId}/status", 204,
                new { isActive = false }, admin.AccessToken);
            using var disabledAccess = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users/me", 401, bearer: newSession.AccessToken);
            using var enabled = await SendAsync(client, HttpMethod.Put, $"/api/v1/identity/users/{userId}/status", 204,
                new { isActive = true }, admin.AccessToken);
            using var oldRefreshAfterEnable = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/refresh", 401, cookie: newSession.Cookie);
            var logoutSession = await LoginAsync(client, "mentee@checks.test", "NewPassword123!");
            var otherSession = await LoginAsync(client, "mentee@checks.test", "NewPassword123!");
            using var logout = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/logout", 204,
                bearer: logoutSession.AccessToken, cookie: logoutSession.Cookie);
            using var accessAfterLogout = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users/me", 401, bearer: logoutSession.AccessToken);
            using var otherAccessAfterLogout = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users/me", 401, bearer: otherSession.AccessToken);
            var freshSession = await LoginAsync(client, "mentee@checks.test", "NewPassword123!");
            using var refreshAfterLogout = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/refresh", 401, cookie: logoutSession.Cookie);
            using var otherRefreshAfterLogout = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/refresh", 401, cookie: otherSession.Cookie);
            using var freshAccessAfterReplay = await SendAsync(client, HttpMethod.Get, "/api/v1/identity/users/me", 200, bearer: freshSession.AccessToken);
            using var freshRefreshAfterReplay = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/refresh", 200, cookie: freshSession.Cookie);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task<string> CheckConcurrentRefreshAsync(IServiceProvider services, string rawToken, Guid userId)
    {
        await using var left = services.CreateAsyncScope();
        await using var right = services.CreateAsyncScope();
        var hash = RefreshToken.Hash(rawToken);
        // Preload both original versions so this exercises the optimistic concurrency guard deterministically.
        await left.ServiceProvider.GetRequiredService<IdentityDbContext>().RefreshTokens.SingleAsync(token => token.Token == hash);
        await right.ServiceProvider.GetRequiredService<IdentityDbContext>().RefreshTokens.SingleAsync(token => token.Token == hash);
        var results = await Task.WhenAll(
            left.ServiceProvider.GetRequiredService<ISender>().Send(new RefreshTokenCommand(rawToken)),
            right.ServiceProvider.GetRequiredService<ISender>().Send(new RefreshTokenCommand(rawToken)));
        Program.Check(results.Count(result => result.IsSuccess) == 1, "Concurrent refresh must produce exactly one successful rotation.");
        await using var verification = services.CreateAsyncScope();
        var tokens = await verification.ServiceProvider.GetRequiredService<IdentityDbContext>().RefreshTokens.Where(token => token.UserId == userId).ToListAsync();
        Program.Check(tokens.Count == 2 && tokens.All(token => !token.IsActive), "Concurrent reuse must revoke every token in the account.");
        Program.Check(tokens.Select(token => token.ExpiresAtUtc).Distinct().Count() == 1, "Rotation extended the absolute refresh expiration.");
        Program.Check(tokens.All(token => token.Token.Length == 64 && token.Token != rawToken), "Database contains a raw refresh token.");
        return results.Single(result => result.IsSuccess).Value.AccessToken;
    }

    private static async Task CheckFailedLoginLockoutAsync(IServiceProvider services)
    {
        const string email = "lockout@checks.test";
        await using (var seed = services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Users.Add(User.Create(email, seed.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword("Password123!"), "Lockout"));
            await db.SaveChangesAsync();
        }
        var attempts = Enumerable.Range(0, 5).Select(_ => services.CreateAsyncScope()).ToArray();
        try
        {
            // Each handler starts with the same stale count; only an atomic database increment preserves all attempts.
            await Task.WhenAll(attempts.Select(scope => scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.SingleAsync(user => user.Email == email)));
            var results = await Task.WhenAll(attempts.Select(scope => scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new LoginCommand(email, "Incorrect123!"))));
            Program.Check(results.All(result => result.IsFailure), "Incorrect concurrent logins were accepted.");
        }
        finally { foreach (var scope in attempts) await scope.DisposeAsync(); }

        await using var verify = services.CreateAsyncScope();
        var context = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var locked = await context.Users.AsNoTracking().SingleAsync(user => user.Email == email);
        Program.Check(locked.AccessFailedCount == 5 && locked.IsLockedOut, "Concurrent failed login increments were lost or did not lock the account.");
        var sender = verify.ServiceProvider.GetRequiredService<ISender>();
        Program.Check((await sender.Send(new LoginCommand(email, "Password123!"))).IsFailure, "Correct password bypassed active lockout.");
        await context.Users.Where(user => user.Email == email).ExecuteUpdateAsync(setters => setters.SetProperty(user => user.LockoutEndUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        context.ChangeTracker.Clear();
        Program.Check((await sender.Send(new LoginCommand(email, "Incorrect123!"))).IsFailure, "Incorrect password after lockout expiry was accepted.");
        var afterExpiry = await context.Users.AsNoTracking().SingleAsync(user => user.Email == email);
        Program.Check(afterExpiry.AccessFailedCount == 1 && !afterExpiry.IsLockedOut, "Expired lockout did not start a new failed-attempt window.");
        context.ChangeTracker.Clear();
        Program.Check((await sender.Send(new LoginCommand(email, "Password123!"))).IsSuccess, "Valid login failed after lockout expired.");
        var reset = await context.Users.AsNoTracking().SingleAsync(user => user.Email == email);
        Program.Check(reset.AccessFailedCount == 0 && reset.LockoutEndUtc is null, "Successful login did not clear failed attempts and lockout.");
    }

    private static async Task CheckCatalogAsync(HttpClient client, string admin, string mentee)
    {
        const string categories = "/api/v1/catalog/categories";
        const string tags = "/api/v1/catalog/tags";
        const string skills = "/api/v1/catalog/skills";
        using var denied = await SendAsync(client, HttpMethod.Post, categories, 403, new { name = "Denied" }, mentee);
        using var created = await SendAsync(client, HttpMethod.Post, categories, 201, new { name = "Lập trình" }, admin);
        var root = (await JsonAsync(created)).GetProperty("id").GetGuid();
        using var childCreated = await SendAsync(client, HttpMethod.Post, categories, 201, new { name = "Backend", parentId = root }, admin);
        var child = (await JsonAsync(childCreated)).GetProperty("id").GetGuid();
        using var cycle = await SendAsync(client, HttpMethod.Put, $"{categories}/{root}", 409, new { name = "Lập trình", parentId = child, isActive = true }, admin);
        using var nonempty = await SendAsync(client, HttpMethod.Delete, $"{categories}/{root}", 409, bearer: admin);
        using var tree = await SendAsync(client, HttpMethod.Get, $"{categories}?isTree=true", 200);
        Program.Check((await JsonAsync(tree)).GetProperty("items")[0].GetProperty("children").GetArrayLength() == 1, "Category tree is incorrect.");
        using var tagCreated = await SendAsync(client, HttpMethod.Post, tags, 201, new { name = "API" }, admin);
        var tag = (await JsonAsync(tagCreated)).GetProperty("id").GetGuid();
        using var duplicateTag = await SendAsync(client, HttpMethod.Post, tags, 409, new { name = "api" }, admin);
        using var missingTag = await SendAsync(client, HttpMethod.Post, skills, 400,
            new { name = "Missing tag", categoryId = child, tagIds = new[] { Guid.NewGuid() } }, admin);
        using var skillCreated = await SendAsync(client, HttpMethod.Post, skills, 201,
            new { name = "ASP.NET", categoryId = child, tagIds = new[] { tag, tag } }, admin);
        var skill = (await JsonAsync(skillCreated)).GetProperty("id").GetGuid();
        using var categoryRead = await SendAsync(client, HttpMethod.Get, $"{categories}/{child}", 200);
        Program.Check((await JsonAsync(categoryRead)).GetProperty("skills").GetArrayLength() == 1, "Category details omitted its skill.");
        using var skillRead = await SendAsync(client, HttpMethod.Get, $"{skills}/{skill}", 200);
        Program.Check((await JsonAsync(skillRead)).GetProperty("tags").GetArrayLength() == 1, "Skill tags were not deduplicated.");
        using var tagsRead = await SendAsync(client, HttpMethod.Get, tags, 200);
        Program.Check((await JsonAsync(tagsRead)).GetProperty("items").GetArrayLength() == 1, "Tags API omitted its tag.");
        using var searchRead = await SendAsync(client, HttpMethod.Get, $"{skills}?search=ASP.NET", 200);
        Program.Check((await JsonAsync(searchRead)).GetProperty("totalCount").GetInt64() == 1, "Skill search did not return its match.");
        using var wildcardRead = await SendAsync(client, HttpMethod.Get, $"{skills}?search=%25", 200);
        Program.Check((await JsonAsync(wildcardRead)).GetProperty("totalCount").GetInt64() == 0, "Search treated a literal percent as a wildcard.");
        using var duplicateSkill = await SendAsync(client, HttpMethod.Post, skills, 409,
            new { name = "ASP.NET", categoryId = child }, admin);
        using var skillUpdated = await SendAsync(client, HttpMethod.Put, $"{skills}/{skill}", 204,
            new { name = "ASP.NET", categoryId = child, isActive = true, tagIds = Array.Empty<Guid>() }, admin);
        using var replacedTags = await SendAsync(client, HttpMethod.Get, $"{skills}/{skill}", 200);
        Program.Check((await JsonAsync(replacedTags)).GetProperty("tags").GetArrayLength() == 0, "Skill update did not remove old tag associations.");
        using var invalidPage = await SendAsync(client, HttpMethod.Get, $"{skills}?page=2147483647&pageSize=100", 400);
        using var emptyCreated = await SendAsync(client, HttpMethod.Post, categories, 201, new { name = "Empty" }, admin);
        var empty = (await JsonAsync(emptyCreated)).GetProperty("id").GetGuid();
        using var deleted = await SendAsync(client, HttpMethod.Delete, $"{categories}/{empty}", 204, bearer: admin);
        using var deletedRead = await SendAsync(client, HttpMethod.Get, $"{categories}/{empty}", 404);
        using var deletedParent = await SendAsync(client, HttpMethod.Post, categories, 400, new { name = "Orphan", parentId = empty }, admin);

        using var xCreated = await SendAsync(client, HttpMethod.Post, categories, 201, new { name = "Concurrent X" }, admin);
        using var yCreated = await SendAsync(client, HttpMethod.Post, categories, 201, new { name = "Concurrent Y" }, admin);
        var x = (await JsonAsync(xCreated)).GetProperty("id").GetGuid();
        var y = (await JsonAsync(yCreated)).GetProperty("id").GetGuid();
        var moves = await Task.WhenAll(
            SendAsync(client, HttpMethod.Put, $"{categories}/{x}", null, new { name = "Concurrent X", parentId = y, isActive = true }, admin),
            SendAsync(client, HttpMethod.Put, $"{categories}/{y}", null, new { name = "Concurrent Y", parentId = x, isActive = true }, admin));
        try
        {
            Program.Check(moves.Count(response => response.StatusCode == HttpStatusCode.NoContent) == 1 &&
                moves.Count(response => response.StatusCode == HttpStatusCode.Conflict) == 1,
                "Concurrent category moves must reject a cycle with one success and one conflict.");
        }
        finally { foreach (var response in moves) response.Dispose(); }
    }

    private static async Task<(string AccessToken, string Cookie, string RefreshToken)> LoginAsync(HttpClient client, string email, string password = "Password123!")
    {
        using var response = await SendAsync(client, HttpMethod.Post, "/api/v1/identity/auth/login", 200, new { email, password });
        var json = await JsonAsync(response);
        Program.Check(!json.TryGetProperty("refreshToken", out _), "Raw refresh token leaked in HTTP body.");
        var header = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("refreshToken=", StringComparison.Ordinal));
        Program.Check(header.Contains("httponly", StringComparison.OrdinalIgnoreCase) && header.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            header.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) && header.Contains("path=/api/v1/identity/auth", StringComparison.OrdinalIgnoreCase),
            "Refresh cookie flags/path violate the contract.");
        var cookie = header.Split(';')[0];
        return (json.GetProperty("accessToken").GetString()!, cookie, Uri.UnescapeDataString(cookie["refreshToken=".Length..]));
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, int? expected,
        object? body = null, string? bearer = null, string? cookie = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request);
        if (expected.HasValue && (int)response.StatusCode != expected.Value)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new InvalidOperationException($"{method} {path}: expected {expected}, received {status}.");
        }
        return response;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }
}
