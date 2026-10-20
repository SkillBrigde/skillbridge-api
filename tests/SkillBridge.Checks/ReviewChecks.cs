using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Modules.Booking.Infrastructure;
using SkillBridge.Modules.Learning.Infrastructure;
using SkillBridge.Modules.Reviews.Domain;
using SkillBridge.Modules.Reviews.Infrastructure;

namespace SkillBridge.Checks;

internal static class ReviewChecks
{
    public static void CheckDomain()
    {
        var now = DateTimeOffset.UtcNow; var author = Guid.NewGuid(); var mentor = Guid.NewGuid();
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), mentor, author, 5, 4, 3, 2, "Nhận xét", now).Value;
        Program.Check(review.Update(author, 0, null, null, null, "x", now).IsFailure && review.OverallRating == 5, "Invalid edit changed ratings.");
        Program.Check(review.Update(author, 4, null, null, null, "Sửa", now.AddHours(48).AddTicks(-1)).IsSuccess, "Edit window closed early.");
        Program.Check(review.Update(author, 1, null, null, null, "Muộn", now.AddHours(48)).IsFailure && review.OverallRating == 4, "48-hour boundary accepted.");
        Program.Check(review.Update(Guid.NewGuid(), 1, null, null, null, "Sai chủ", now).IsFailure && review.Reply(Guid.NewGuid(), "Sai mentor", now).IsFailure, "Review ownership bypassed.");
    }
    public static async Task RunAsync(HttpClient client, IServiceProvider services, string token)
    {
        try { await CoreAsync(client, services, token); }
        finally { LearningChecks.SetTime(null); }
    }
    private static async Task CoreAsync(HttpClient client, IServiceProvider services, string token)
    {
        Guid booking; Guid session; Guid mentor; DateTimeOffset now;
        await using (var scope = services.CreateAsyncScope())
        {
            var order = await scope.ServiceProvider.GetRequiredService<BookingDbContext>().Bookings.AsNoTracking().SingleAsync(x => x.Status == "Completed");
            booking = order.Id; mentor = order.Terms.MentorId;
            var sessions = await scope.ServiceProvider.GetRequiredService<LearningDbContext>().Sessions.AsNoTracking().Where(x => x.BookingId == booking).ToListAsync();
            session = sessions[0].Id; now = sessions.Max(x => x.ActualEndUtc!.Value).AddMinutes(1);
        }
        LearningChecks.SetTime(now);
        var teacher = await DatabaseChecks.LoginAsync(client, "learning-mentor@checks.test");
        var stranger = await MessagingChecks.SeedAsync(services, "reviews-stranger@checks.test", "Mentee");
        var otherTeacher = await MessagingChecks.SeedAsync(services, "reviews-other-mentor@checks.test", "Mentor");
        const string root = "/api/v1/reviews";
        using var pending = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/my-pending", 200, bearer: token);
        Program.Check((await DatabaseChecks.JsonAsync(pending)).GetProperty("items").EnumerateArray().Any(x => x.GetProperty("bookingId").GetGuid() == booking), "Completed package absent from pending reviews.");
        object Payload(Guid book, Guid sess, Guid teacherId, int rating = 5) => new
        {
            bookingId = book,
            sessionId = sess,
            mentorId = teacherId,
            overallRating = rating,
            communicationRating = 4,
            expertiseRating = 3,
            helpfulnessRating = 2,
            comment = "Review đã xác minh"
        };
        using var foreign = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root, 404, Payload(booking, session, mentor), stranger.Token);
        using var wrongSession = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root, 409, Payload(booking, Guid.NewGuid(), mentor), token);
        using var wrongMentor = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root, 409, Payload(booking, session, Guid.NewGuid()), token);
        using var invalid = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root, 400, Payload(booking, session, mentor, 6), token);
        Guid incomplete;
        await using (var scope = services.CreateAsyncScope()) incomplete = await scope.ServiceProvider.GetRequiredService<BookingDbContext>().Bookings.Where(x => x.Status != "Completed").Select(x => x.Id).FirstAsync();
        using var notCompleted = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root, 409, Payload(incomplete, session, mentor), token);
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => DatabaseChecks.SendAsync(client, HttpMethod.Post, root, null, Payload(booking, session, mentor), token)));
        Guid reviewId;
        try
        {
            Program.Check(responses.Count(x => (int)x.StatusCode == 201) == 1 && responses.Count(x => (int)x.StatusCode == 409) == 2, "Concurrent create produced multiple reviews.");
            reviewId = (await DatabaseChecks.JsonAsync(responses.Single(x => (int)x.StatusCode == 201))).GetProperty("id").GetGuid();
        }
        finally { foreach (var response in responses) response.Dispose(); }
        using var publicView = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/" + reviewId, 200);
        var json = await DatabaseChecks.JsonAsync(publicView);
        Program.Check(!json.TryGetProperty("menteeUserId", out _) && !json.TryGetProperty("bookingId", out _) && !json.TryGetProperty("sessionId", out _), "Public review disclosed private booking/participant identifiers.");
        using var foreignEdit = await DatabaseChecks.SendAsync(client, HttpMethod.Put, root + "/" + reviewId, 404, new { overallRating = 1, comment = "Sai chủ" }, stranger.Token);
        using var edit = await DatabaseChecks.SendAsync(client, HttpMethod.Put, root + "/" + reviewId, 204, new { overallRating = 4, comment = "Cập nhật" }, token);
        using var replyForeign = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/" + reviewId + "/reply", 404, new { reply = "Sai mentor" }, otherTeacher.Token);
        using var reply = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/" + reviewId + "/reply", 204, new { reply = "Cảm ơn" }, teacher.AccessToken);
        using var summary = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/mentors/{mentor}/summary", 200);
        var stats = await DatabaseChecks.JsonAsync(summary);
        Program.Check(stats.GetProperty("totalReviews").GetInt64() == 1 && stats.GetProperty("averageOverall").GetDouble() == 4 && stats.GetProperty("averageCommunication").GetDouble() == 4,
            "Summary did not reflect updated ratings or omitted criteria were reset.");
        using var list = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/mentors/{mentor}", 200);
        using var badPage = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/mentors/{mentor}?pageSize=101", 400);
        using var after = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/my-pending", 200, bearer: token);
        Program.Check(!(await DatabaseChecks.JsonAsync(after)).GetProperty("items").EnumerateArray().Any(x => x.GetProperty("bookingId").GetGuid() == booking), "Reviewed booking remained pending.");
        LearningChecks.SetTime(now.AddHours(48));
        using var tooLate = await DatabaseChecks.SendAsync(client, HttpMethod.Put, root + "/" + reviewId, 409, new { overallRating = 1, comment = "Muộn" }, token);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReviewsDbContext>();
            Program.Check(await db.Reviews.CountAsync() == 1 && await db.OutboxMessages.CountAsync() == 3, "Failed writes emitted events or duplicate reviews.");
        }
        Console.WriteLine("PASS: Reviews complete-package evidence, one-per-booking races, owner edits, 48-hour boundary, mentor replies, public privacy and live summary.");
    }
}
