using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Modules.Catalog.Domain;
using SkillBridge.Modules.Catalog.Infrastructure;
using SkillBridge.Modules.Identity.Infrastructure.Persistence;
using SkillBridge.Modules.Learning.Infrastructure;
using SkillBridge.Modules.Profiles.Domain;
using SkillBridge.Modules.Profiles.Infrastructure;
using SkillBridge.Modules.Recommendations.Application;
using SkillBridge.Modules.Recommendations.Domain;
using SkillBridge.Modules.Recommendations.Infrastructure;

namespace SkillBridge.Checks;

internal static class RecommendationChecks
{
    public static void CheckDomain()
    {
        var metric = MentorMetric.Create(Guid.NewGuid()); var skill = Guid.NewGuid();
        metric.Update(Guid.NewGuid(), [skill], [new ServicePrice(500000, "VND")], 0, 0, 0, null, 0, 0, null, 0, DateTimeOffset.UtcNow);
        Program.Check(metric.AcceptanceRate is null && metric.CompletionRate is null && metric.AverageRating is null, "Cold start fabricated mentor metrics.");
        var baseline = RecommendationScoring.Calculate(metric, new Dictionary<Guid, int>(), null, null, null).Score;
        var interested = RecommendationScoring.Calculate(metric, new Dictionary<Guid, int> { [skill] = 1 }, null, null, null).Score;
        var vnd = RecommendationScoring.Calculate(metric, new Dictionary<Guid, int>(), 400000, 600000, "VND").Score;
        var usd = RecommendationScoring.Calculate(metric, new Dictionary<Guid, int>(), 400000, 600000, "USD").Score;
        Program.Check(interested > baseline && vnd > baseline && usd == baseline && interested <= 1, "Ranking ignored interests, mixed currencies or exceeded bounds.");
    }
    public static async Task RunAsync(HttpClient client, IServiceProvider services, string token, string admin)
    {
        try { await CoreAsync(client, services, token, admin); }
        finally { LearningChecks.SetTime(null); }
    }
    private static async Task CoreAsync(HttpClient client, IServiceProvider services, string token, string admin)
    {
        Guid mentor; Guid otherMentor; Guid otherUser; Guid skillId;
        await using (var scope = services.CreateAsyncScope())
        {
            var identities = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var owner = await identities.Users.Where(x => x.Email == "learning-mentor@checks.test").Select(x => x.Id).SingleAsync();
            otherUser = await identities.Users.Where(x => x.Email == "payments-mentor@checks.test").Select(x => x.Id).SingleAsync();
            var profiles = scope.ServiceProvider.GetRequiredService<ProfilesDbContext>();
            mentor = await profiles.MentorProfiles.Where(x => x.UserId == owner).Select(x => x.Id).SingleAsync();
            otherMentor = await profiles.MentorProfiles.Where(x => x.UserId == otherUser).Select(x => x.Id).SingleAsync();
            var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var category = Category.Create("Recommendation checks", "Checks", null, null, 0).Value;
            catalog.Categories.Add(category); await catalog.SaveChangesAsync();
            var skill = Skill.Create(category.Id, "Recommendation skill", "Checks", []).Value;
            catalog.Skills.Add(skill); await catalog.SaveChangesAsync(); skillId = skill.Id;
            profiles.MentorSkills.AddRange(MentorSkill.Create(mentor, skillId, skill.Name, "Expert", 5, DateTimeOffset.UtcNow).Value,
                MentorSkill.Create(otherMentor, skillId, skill.Name, "Advanced", 3, DateTimeOffset.UtcNow).Value);
            await profiles.SaveChangesAsync();
            var end = await scope.ServiceProvider.GetRequiredService<LearningDbContext>().Sessions.MaxAsync(x => x.ActualEndUtc);
            LearningChecks.SetTime(end!.Value.AddMinutes(2));
            Program.Check((await scope.ServiceProvider.GetRequiredService<RecommendationsService>().RefreshMetricsAsync(default)).IsSuccess, "Metrics batch failed.");
        }
        var teacher = await DatabaseChecks.LoginAsync(client, "learning-mentor@checks.test");
        var otherTeacher = await DatabaseChecks.LoginAsync(client, "payments-mentor@checks.test");
        const string root = "/api/v1/recommendations";
        using var anonymous = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/mentors", 401);
        using var wrongRole = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/mentors", 403, bearer: teacher.AccessToken);
        using var emptySkill = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/mentors/for-skill/" + Guid.NewGuid(), 404);
        using var forSkill = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/mentors/for-skill/" + skillId, 200);
        Program.Check((await DatabaseChecks.JsonAsync(forSkill)).GetProperty("items").GetArrayLength() == 2, "Skill candidates did not use active Catalog/Profiles data.");
        using var mine = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/mentors/{mentor}/metrics", 200, bearer: teacher.AccessToken);
        var metrics = await DatabaseChecks.JsonAsync(mine);
        Program.Check(metrics.GetProperty("totalSessions").GetInt32() == 2 && metrics.GetProperty("averageRating").GetDouble() == 4 && metrics.GetProperty("totalReviews").GetInt32() == 1,
            "Metrics did not use actual Learning completion and current Reviews rating.");
        using var foreignMetrics = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/mentors/{mentor}/metrics", 403, bearer: otherTeacher.AccessToken);
        using var adminMetrics = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/mentors/{mentor}/metrics", 200, bearer: admin);
        using var cold = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/mentors/{otherMentor}/metrics", 200, bearer: otherTeacher.AccessToken);
        Program.Check((await DatabaseChecks.JsonAsync(cold)).GetProperty("averageRating").ValueKind == System.Text.Json.JsonValueKind.Null, "Missing reviews became an invented average rating.");
        using var before = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/mentors", 200, bearer: token);
        var beforeScore = (await DatabaseChecks.JsonAsync(before)).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("mentorId").GetGuid() == mentor).GetProperty("score").GetDouble();
        using var badType = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/interactions", 400, new { mentorId = mentor, skillId, interactionType = "FakeBooking" }, token);
        using var badSkill = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/interactions", 400, new { mentorId = mentor, skillId = Guid.NewGuid(), interactionType = "Click" }, token);
        var interactions = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/interactions", 204,
            new { mentorId = mentor, skillId, interactionType = "Click" }, token)));
        foreach (var response in interactions) response.Dispose();
        using var noSkill = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/interactions", 204, new { mentorId = mentor, interactionType = "ViewProfile" }, token);
        using var after = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/mentors", 200, bearer: token);
        var afterScore = (await DatabaseChecks.JsonAsync(after)).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("mentorId").GetGuid() == mentor).GetProperty("score").GetDouble();
        Program.Check(afterScore > beforeScore && afterScore <= 1, "Valid interest did not affect personalized score.");
        using var trending = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/trending-skills", 200);
        Program.Check((await DatabaseChecks.JsonAsync(trending)).EnumerateArray().Single(x => x.GetProperty("skillId").GetGuid() == skillId).GetProperty("trendingScore").GetInt32() == 1,
            "Duplicate interactions inflated trending.");
        using var badPage = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/mentors?page=2147483647&pageSize=100", 400, bearer: token);
        // Snapshot vẫn tồn tại nhưng Mentor bị khóa phải biến mất ngay khỏi gợi ý.
        using var disable = await DatabaseChecks.SendAsync(client, HttpMethod.Put, $"/api/v1/identity/users/{otherUser}/status", 204, new { isActive = false }, admin);
        using var filtered = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/mentors/for-skill/" + skillId, 200);
        Program.Check(!(await DatabaseChecks.JsonAsync(filtered)).GetProperty("items").EnumerateArray().Any(x => x.GetProperty("mentorId").GetGuid() == otherMentor), "Suspended Mentor leaked through cached metrics.");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecommendationsDbContext>();
            Program.Check(await db.Interactions.CountAsync() == 2 && await db.Logs.CountAsync() == 2, "Interaction race or recommendation log persistence failed.");
        }
        Console.WriteLine("PASS: Recommendations real metrics, role/owner permissions, skill binding, personalized ranking, deduplicated interactions/trending and immediate suspension filtering.");
    }
}
