using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Catalog.Application;
using SkillBridge.Modules.Catalog.Domain;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Checks;

internal static class MentorServiceChecks
{
    private static readonly MentorServiceRequest Package = new("Lộ trình backend", "Nội dung gói", "Tier3_Roadmap",
        60, 2_000_000m, "VND", 4, "Lộ trình và ghi chú", false);

    public static void CheckDomain()
    {
        Program.Check(MentorServiceRules.Price(100_000_000m, "VND") && !MentorServiceRules.Price(1.1m, "VND") &&
            MentorServiceRules.Price(5000m, "USD") && !MentorServiceRules.Price(1.001m, "USD"), "Service money boundaries failed.");
        var invalid = MentorService.Create(Guid.NewGuid(), Guid.NewGuid(), "Valid", "Valid", "Tier4", 60,
            10, "USD", 4, "Valid", true, DateTimeOffset.UtcNow);
        Program.Check(invalid.IsFailure, "Domain accepted an unsupported service tier.");
    }

    public static async Task RunAsync(HttpClient client, IServiceProvider services, string menteeToken)
    {
        const string route = "/api/v1/catalog/services";
        var (ownerId, token) = await SeedUserAsync(services, "services-owner@checks.test");
        var (_, stranger) = await SeedUserAsync(services, "services-stranger@checks.test");
        using var anonymous = await DatabaseChecks.SendAsync(client, HttpMethod.Post, route, 401, Package);
        using var forbidden = await DatabaseChecks.SendAsync(client, HttpMethod.Post, route, 403, Package, menteeToken);
        using var missingProfile = await DatabaseChecks.SendAsync(client, HttpMethod.Post, route, 409, Package, token);
        using var profile = await DatabaseChecks.SendAsync(client, HttpMethod.Put, "/api/v1/profiles/mentors/me", 204,
            new
            {
                headline = "Mentor",
                bio = "Backend mentor",
                hourlyRate = 500000,
                currency = "VND",
                timezone = "Asia/Ho_Chi_Minh",
                isAcceptingMentees = true
            }, token);
        using var profileRead = await DatabaseChecks.SendAsync(client, HttpMethod.Get, "/api/v1/profiles/mentors/me", 200, bearer: token);
        var profileId = (await DatabaseChecks.JsonAsync(profileRead)).GetProperty("id").GetGuid();
        Program.Check(profileId != ownerId, "Test must cover distinct profile and Identity IDs.");

        foreach (var invalid in new[] { Package with { Price = 1.1m }, Package with { TotalSessions = 0 },
            Package with { DurationMinutes = 241 }, Package with { IsActive = null }, Package with { Currency = "EUR" },
            Package with { ServiceType = "Tier4" }, Package with { Currency = "USD", Price = 0.001m } })
        {
            using var rejected = await DatabaseChecks.SendAsync(client, HttpMethod.Post, route, 400, invalid, token);
            Program.Check((await DatabaseChecks.JsonAsync(rejected)).TryGetProperty("errors", out _), "Service validation must include field errors.");
        }
        var injected = JsonSerializer.SerializeToNode(Package, JsonSerializerOptions.Web)!;
        injected["mentorId"] = Guid.NewGuid();
        using var ownerInjection = await DatabaseChecks.SendAsync(client, HttpMethod.Post, route, 400, injected, token);
        using var created = await DatabaseChecks.SendAsync(client, HttpMethod.Post, route, 201, Package, token);
        var json = await DatabaseChecks.JsonAsync(created);
        var id = json.GetProperty("id").GetGuid();
        Program.Check(created.Headers.ETag?.Tag == "\"1\"" && json.GetProperty("mentorId").GetGuid() == profileId &&
            !json.TryGetProperty("mentorUserId", out _), "Service ownership DTO or ETag is wrong.");
        using var inactive = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{route}/{id}", 404);
        using var own = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{route}/me", 200, bearer: token);
        Program.Check((await DatabaseChecks.JsonAsync(own)).GetProperty("totalCount").GetInt64() == 1, "Owner cannot find inactive service.");
        using var publicHidden = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"/api/v1/catalog/mentors/{profileId}/services", 200);
        Program.Check((await DatabaseChecks.JsonAsync(publicHidden)).GetProperty("totalCount").GetInt64() == 0, "Public list leaked inactive service.");
        using var other = await WriteAsync(client, HttpMethod.Put, id, stranger, Package, "\"1\"", 404);
        using var missingVersion = await WriteAsync(client, HttpMethod.Put, id, token, Package, null, 428);
        using var weak = await WriteAsync(client, HttpMethod.Put, id, token, Package, "W/\"1\"", 400);
        using var activate = await WriteAsync(client, HttpMethod.Put, id, token, Package with { IsActive = true }, "\"1\"", 200);
        using var active = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{route}/{id}", 200);
        Program.Check(active.Headers.ETag?.Tag == "\"2\"", "Version did not advance after activation.");
        using var pageError = await DatabaseChecks.SendAsync(client, HttpMethod.Get,
            $"/api/v1/catalog/mentors/{profileId}/services?page=2147483647&pageSize=100", 400);
        using var unsupportedType = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{route}/me?serviceType=unknown", 400, bearer: token);
        MentorServiceSnapshot snapshot;
        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<MentorServices>().GetBookingSnapshotAsync(id, default);
            Program.Check(result.IsSuccess && result.Value.MentorUserId == ownerId && result.Value.MentorId == profileId &&
                result.Value.Price == 2_000_000m && result.Value.TotalSessions == 4, "Package snapshot must preserve whole-package price and trusted IDs.");
            snapshot = result.Value;
        }
        var race = await Task.WhenAll(
            WriteAsync(client, HttpMethod.Put, id, token, Package with { Price = 3_000_000m, IsActive = true }, "\"2\"", null),
            WriteAsync(client, HttpMethod.Put, id, token, Package with { Price = 4_000_000m, IsActive = true }, "\"2\"", null));
        try
        {
            Program.Check(race.Count(x => (int)x.StatusCode == 200) == 1 && race.Count(x => (int)x.StatusCode == 412) == 1,
                "Concurrent service updates must have exactly one winner.");
        }
        finally { foreach (var response in race) response.Dispose(); }
        using var staleDelete = await WriteAsync(client, HttpMethod.Delete, id, token, null, "\"2\"", 412);
        using var deleted = await WriteAsync(client, HttpMethod.Delete, id, token, null, "\"3\"", 204);
        using var missing = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{route}/{id}", 404);
        using var ownAfterDelete = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{route}/me", 200, bearer: token);
        Program.Check((await DatabaseChecks.JsonAsync(ownAfterDelete)).GetProperty("totalCount").GetInt64() == 0 &&
            snapshot.Price == 2_000_000m && snapshot.ServiceVersion == 2, "Deletion changed an existing snapshot or leaked a deleted service.");
        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<MentorServices>().GetBookingSnapshotAsync(id, default);
            Program.Check(result.IsFailure, "Deleted service remained bookable.");
            var user = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.SingleAsync(x => x.Id == ownerId);
            user.SetStatus(false);
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().SaveChangesAsync();
        }
        using var disabled = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{route}/me", 401, bearer: token);
        Console.WriteLine("PASS: Mentor services ownership, money, multi-session snapshots, visibility and optimistic concurrency.");
    }

    private static async Task<(Guid Id, string Token)> SeedUserAsync(IServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var user = User.Create(email, scope.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword("Password123!"), "Service Mentor", "Mentor");
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>().GenerateAccessToken(
            user.Id, user.Email, user.FullName, [user.Role], user.SecurityStamp));
    }

    private static async Task<HttpResponseMessage> WriteAsync(HttpClient client, HttpMethod method, Guid id,
        string token, object? body, string? version, int? expected)
    {
        using var request = new HttpRequestMessage(method, $"/api/v1/catalog/services/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", version);
        if (body is not null) request.Content = JsonContent.Create(body);
        var response = await client.SendAsync(request);
        Program.Check(!expected.HasValue || (int)response.StatusCode == expected, $"Service write expected {expected}, got {(int)response.StatusCode}.");
        return response;
    }
}
