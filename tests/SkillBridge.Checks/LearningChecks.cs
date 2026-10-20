using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SkillBridge.Api.Common;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Application;
using SkillBridge.Modules.Booking.Infrastructure;
using SkillBridge.Modules.Learning.Application;
using SkillBridge.Modules.Learning.Domain;
using SkillBridge.Modules.Learning.Infrastructure;

namespace SkillBridge.Checks;

internal static class LearningChecks
{
    private static readonly AdjustableClock Clock = new();
    internal static void SetTime(DateTimeOffset? now) => Clock.Now = now;
    public static void ConfigureServices(IServiceCollection services) => services.AddSingleton<TimeProvider>(Clock);
    public static void CheckDomain()
    {
        var start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var mentor = Guid.NewGuid(); var mentee = Guid.NewGuid();
        var session = LearningSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), mentor, mentee, 1, start, start.AddHours(1), start).Value;
        Program.Check(session.Start(mentor, "https://evil.example/room", start).IsFailure, "Untrusted room URL accepted.");
        Program.Check(session.Start(mentor, "https://meet.google.com/abc-defg-hij", start.AddSeconds(-1)).IsFailure, "Session started before its scheduled time.");
        Program.Check(session.Start(mentor, "https://meet.google.com/abc-defg-hij", start).IsSuccess, "Valid room/start rejected.");
        Program.Check(session.End(mentor, [], start.AddHours(1)).IsFailure, "Session completed without participant attendance.");
        var a = SessionAttendance.Join(session, mentor, start.AddMinutes(1)).Value;
        var b = SessionAttendance.Join(session, mentee, start.AddMinutes(2)).Value;
        Program.Check(session.End(mentor, [a, b], start.AddMinutes(59)).IsFailure && session.Status == "InProgress", "Premature end changed the session.");
        Program.Check(session.End(mentor, [a, b], start.AddHours(1)).IsSuccess && a.LeftAtUtc.HasValue && b.LeftAtUtc.HasValue,
            "Completed session did not close both attendances.");
        Program.Check(session.Sync(start.AddHours(2), start.AddHours(3), false).IsFailure, "Sync rewrote completed attendance history.");
        Program.Check(SessionActionItem.Create(session, mentor, Guid.NewGuid(), "Bài tập", null, null, start).IsFailure,
            "Task assigned to somebody outside the booking.");
    }

    public static async Task RunAsync(HttpClient client, IServiceProvider services, string menteeToken, string adminToken)
    {
        try { await RunCoreAsync(client, services, menteeToken, adminToken); }
        finally { Clock.Now = null; }
    }

    private static async Task RunCoreAsync(HttpClient client, IServiceProvider services, string menteeToken, string adminToken)
    {
        var mentor = await MessagingChecks.SeedAsync(services, "learning-mentor@checks.test", "Mentor");
        var stranger = await MessagingChecks.SeedAsync(services, "learning-stranger@checks.test", "Mentee");
        const string root = "/api/v1/learning/sessions";
        using var noAuth = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root, 401);
        using var profile = await DatabaseChecks.SendAsync(client, HttpMethod.Put, "/api/v1/profiles/mentors/me", 204,
            new { headline = "Learning Mentor", bio = "Mentor", hourlyRate = 500000, currency = "VND", timezone = "UTC", isAcceptingMentees = true }, mentor.Token);
        using var profileRead = await DatabaseChecks.SendAsync(client, HttpMethod.Get, "/api/v1/profiles/mentors/me", 200, bearer: mentor.Token);
        var mentorId = (await DatabaseChecks.JsonAsync(profileRead)).GetProperty("id").GetGuid();
        using var settings = await DatabaseChecks.SendAsync(client, HttpMethod.Put, "/api/v1/scheduling/settings", 204,
            new { sessionDurationMinutes = 60, bufferTimeMinutes = 0, leadTimeHours = 0, maxDaysInAdvance = 14, timezone = "UTC" }, mentor.Token);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        using var rule = await DatabaseChecks.SendAsync(client, HttpMethod.Post, "/api/v1/scheduling/availability-rules", 201,
            new { dayOfWeek = (int)date.DayOfWeek, startTime = "09:00:00", endTime = "13:00:00" }, mentor.Token);
        using var slotsReply = await DatabaseChecks.SendAsync(client, HttpMethod.Get,
            $"/api/v1/scheduling/mentors/{mentorId}/slots?startDate={date:yyyy-MM-dd}&endDate={date:yyyy-MM-dd}", 200);
        var slotIds = (await DatabaseChecks.JsonAsync(slotsReply)).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        using var service = await DatabaseChecks.SendAsync(client, HttpMethod.Post, "/api/v1/catalog/services", 201,
            new
            {
                title = "Hai buổi học",
                description = "Nội dung",
                serviceType = "Tier3_Roadmap",
                durationMinutes = 60,
                price = 500000,
                currency = "VND",
                totalSessions = 2,
                deliverables = "Tài liệu",
                isActive = true
            }, mentor.Token);
        var serviceJson = await DatabaseChecks.JsonAsync(service);
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/booking/bookings")
        {
            Content = JsonContent.Create(new
            {
                serviceId = serviceJson.GetProperty("id").GetGuid(),
                serviceVersion = serviceJson.GetProperty("version").GetInt64(),
                slotIds = slotIds[..2],
                intakeForm = new { coreQuestion = "Câu hỏi", attachedLinks = "https://example.com/cv", targetGoals = "Mục tiêu" }
            })
        };
        create.Headers.Authorization = new("Bearer", menteeToken); create.Headers.Add("Idempotency-Key", "learning-package");
        using var created = await client.SendAsync(create);
        Program.Check((int)created.StatusCode == 201, "Learning booking fixture could not reserve the package.");
        var booking = await DatabaseChecks.JsonAsync(created);
        var bookingId = booking.GetProperty("id").GetGuid(); var menteeId = booking.GetProperty("menteeUserId").GetGuid();
        await using (var scope = services.CreateAsyncScope())
            Program.Check((await scope.ServiceProvider.GetRequiredService<Bookings>().ReceivePaymentAsync(bookingId, Guid.NewGuid(), 500000, "VND", default)).IsSuccess,
                "Learning fixture payment was rejected.");
        using var confirmed = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"/api/v1/booking/bookings/{bookingId}/confirm", 204, new { }, mentor.Token);
        await DrainAsync(services);
        using var list = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}?bookingId={bookingId}", 200, bearer: menteeToken);
        var sessions = (await DatabaseChecks.JsonAsync(list)).GetProperty("items").EnumerateArray().ToArray();
        Program.Check(sessions.Length == 2, "Confirmed package did not create exactly two learning sessions.");
        var firstId = sessions[0].GetProperty("id").GetGuid();
        using var strangerView = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/{firstId}", 404, bearer: stranger.Token);
        using var privateNote = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{firstId}/notes", 201,
            new { content = "Ghi chú riêng", isPrivate = true }, menteeToken);
        var noteId = (await DatabaseChecks.JsonAsync(privateNote)).GetProperty("id").GetGuid();
        foreach (var viewer in new[] { mentor.Token, adminToken })
        {
            using var hidden = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/{firstId}/notes", 200, bearer: viewer);
            Program.Check((await DatabaseChecks.JsonAsync(hidden)).GetProperty("totalCount").GetInt64() == 0, "Private note leaked to another participant/Admin.");
        }
        using var foreignEdit = await DatabaseChecks.SendAsync(client, HttpMethod.Put, $"{root}/{firstId}/notes/{noteId}", 404,
            new { content = "Ghi đè", isPrivate = false }, mentor.Token);
        using var ownEdit = await DatabaseChecks.SendAsync(client, HttpMethod.Put, $"{root}/{firstId}/notes/{noteId}", 204,
            new { content = "Ghi chú chia sẻ", isPrivate = false }, menteeToken);
        using var foreignDelete = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{root}/{firstId}/notes/{noteId}", 404, bearer: mentor.Token);
        using var ownDelete = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{root}/{firstId}/notes/{noteId}", 204, bearer: menteeToken);
        using var badAssignment = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{firstId}/action-items", 400,
            new { assignedTo = stranger.Id, title = "Bài tập" }, mentor.Token);
        using var task = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{firstId}/action-items", 201,
            new { assignedTo = menteeId, title = "Bài tập", description = "Hoàn thiện API", dueDate = DateTimeOffset.UtcNow.AddDays(10) }, mentor.Token);
        var taskId = (await DatabaseChecks.JsonAsync(task)).GetProperty("id").GetGuid();
        using var teacherPatch = await DatabaseChecks.SendAsync(client, HttpMethod.Patch, $"{root}/{firstId}/action-items/{taskId}", 403, new { isCompleted = true }, mentor.Token);
        using var completedTask = await DatabaseChecks.SendAsync(client, HttpMethod.Patch, $"{root}/{firstId}/action-items/{taskId}", 204, new { isCompleted = true }, menteeToken);
        using var tasks = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/{firstId}/action-items", 200, bearer: menteeToken);
        Program.Check((await DatabaseChecks.JsonAsync(tasks)).GetProperty("items")[0].GetProperty("isCompleted").GetBoolean(), "Assigned Mentee could not complete their task.");
        await CheckMaterialsAsync(client, root, firstId, menteeToken, mentor.Token, stranger.Token);
        var bookedSession = sessions[0].GetProperty("bookingSessionId").GetGuid();
        using var move = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"/api/v1/booking/bookings/{bookingId}/reschedule", 201,
            new { bookingSessionId = bookedSession, newSlotId = slotIds[2], reason = "Dời buổi đầu" }, menteeToken);
        var moveId = (await DatabaseChecks.JsonAsync(move)).GetProperty("id").GetGuid();
        using var approve = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"/api/v1/booking/reschedule-requests/{moveId}/respond", 204, new { isApproved = true }, mentor.Token);
        await DrainAsync(services);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            Program.Check(await db.Sessions.AnyAsync(x => x.Id == firstId && x.ScheduledStartUtc.Hour == 11), "Reschedule changed the learning identity or failed to update its time.");
            var producer = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var confirmedEvents = await producer.OutboxMessages.AsNoTracking().Where(x => x.Type == "Booking.Confirmed.v1").ToListAsync();
            var old = confirmedEvents.Single(x => x.Content.Contains(bookingId.ToString(), StringComparison.Ordinal));
            producer.OutboxMessages.Add(new OutboxMessage { Type = old.Type, Content = old.Content, OccurredOnUtc = DateTimeOffset.UtcNow });
            await producer.SaveChangesAsync();
        }
        await DrainAsync(services);
        using var current = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}?bookingId={bookingId}", 200, bearer: menteeToken);
        sessions = (await DatabaseChecks.JsonAsync(current)).GetProperty("items").EnumerateArray().ToArray();
        Program.Check(sessions.Single(x => x.GetProperty("id").GetGuid() == firstId).GetProperty("scheduledStartUtc").GetDateTimeOffset().Hour == 11,
            "Stale confirmation event rolled back the accepted reschedule.");
        using var early = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{firstId}/start", 409,
            new { roomUrl = "https://meet.google.com/abc-defg-hij" }, mentor.Token);
        for (var index = 0; index < sessions.Length; index++)
        {
            var row = sessions[index]; var sessionId = row.GetProperty("id").GetGuid();
            Clock.Now = row.GetProperty("scheduledStartUtc").GetDateTimeOffset().AddSeconds(1);
            using var beforeStart = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{sessionId}/join", 409, bearer: menteeToken);
            using var badRoom = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{sessionId}/start", 400, new { roomUrl = "https://evil.example/room" }, mentor.Token);
            using var start = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{sessionId}/start", 204, new { roomUrl = "https://meet.google.com/abc-defg-hij" }, mentor.Token);
            using var fixRoom = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{sessionId}/start", 204, new { roomUrl = "https://meet.google.com/xyz-abcd-efg" }, mentor.Token);
            using var roomInfo = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/{sessionId}", 200, bearer: menteeToken);
            Program.Check((await DatabaseChecks.JsonAsync(roomInfo)).GetProperty("roomUrl").GetString() == "https://meet.google.com/xyz-abcd-efg",
                "Mentor could not correct the room link without restarting the session.");
            var joins = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{sessionId}/join", 204, bearer: menteeToken)));
            foreach (var join in joins) join.Dispose();
            using var teacherJoin = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{sessionId}/join", 204, bearer: mentor.Token);
            using var premature = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{sessionId}/end", 409, bearer: mentor.Token);
            Clock.Now = row.GetProperty("scheduledEndUtc").GetDateTimeOffset().AddSeconds(1);
            using var end = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{sessionId}/end", 204, bearer: mentor.Token);
            using var endRetry = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{sessionId}/end", 204, bearer: mentor.Token);
            using var leave = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{sessionId}/leave", 204, bearer: menteeToken);
            using var packageComplete = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"/api/v1/booking/bookings/{bookingId}/complete",
                index == sessions.Length - 1 ? 204 : 409, bearer: menteeToken);
        }
        using var completeRetry = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"/api/v1/booking/bookings/{bookingId}/complete", 204, bearer: mentor.Token);
        await DrainAsync(services);
        await CheckRelayRecoveryAsync(services, bookingId);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            Program.Check(await db.Sessions.CountAsync(x => x.BookingId == bookingId && x.Status == "Completed") == 2, "Package completion did not retain two completed sessions.");
            Program.Check(await db.Attendances.CountAsync(x => x.LeftAtUtc == null) == 0, "Session end left active attendance rows.");
            Program.Check(await db.OutboxMessages.CountAsync(x => x.Type == "Learning.Completed.v1") == 2, "End retry duplicated completion events.");
            Program.Check(await db.OutboxMessages.CountAsync(x => x.Type == "Learning.Started.v1") == 2, "Correcting a room link restarted the session.");
        }
        Console.WriteLine("PASS: Learning privacy, attendance/time windows, assignments, authorized file download, durable outbox/inbox and multi-session booking completion.");
    }

    private static async Task CheckMaterialsAsync(HttpClient client, string root, Guid session, string mentee, string mentor, string stranger)
    {
        var bytes = Encoding.UTF8.GetBytes("Tài liệu buổi học .NET 10");
        async Task<HttpResponseMessage> Upload(string name, byte[] data, string type)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{root}/{session}/materials") { Content = new ByteArrayContent(data) };
            request.Headers.Authorization = new("Bearer", mentee); request.Headers.Add("X-File-Name", name);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(type); request.Headers.ExpectContinue = true;
            return await client.SendAsync(request);
        }
        using var badName = await Upload("%2e%2e%2fsecret.txt", bytes, "text/plain");
        Program.Check((int)badName.StatusCode == 400, "Traversal file name accepted.");
        using var executable = await Upload("script.html", bytes, "text/html");
        Program.Check((int)executable.StatusCode == 400, "Executable file type accepted.");
        using var wrongMime = await Upload("notes.txt", bytes, "application/pdf");
        Program.Check((int)wrongMime.StatusCode == 400, "Mismatched MIME accepted.");
        using var large = await Upload("notes.txt", new byte[LearningSessions.MaxMaterialBytes + 1], "text/plain");
        Program.Check((int)large.StatusCode == 413, "Oversized material accepted.");
        using var uploaded = await Upload(Uri.EscapeDataString("bài-tập.txt"), bytes, "text/plain");
        Program.Check((int)uploaded.StatusCode == 201, "Valid UTF-8 material rejected.");
        var material = await DatabaseChecks.JsonAsync(uploaded); var id = material.GetProperty("id").GetGuid();
        var url = material.GetProperty("downloadUrl").GetString()!;
        using var metadata = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/{session}/materials", 200, bearer: mentor);
        Program.Check(!(await DatabaseChecks.JsonAsync(metadata)).GetProperty("items")[0].TryGetProperty("data", out _), "Materials list exposed raw blob data.");
        using var denied = await DatabaseChecks.SendAsync(client, HttpMethod.Get, url, 404, bearer: stranger);
        using var download = await DatabaseChecks.SendAsync(client, HttpMethod.Get, url, 200, bearer: mentor);
        Program.Check((await download.Content.ReadAsByteArrayAsync()).SequenceEqual(bytes) &&
            download.Content.Headers.ContentDisposition?.DispositionType == "attachment" && download.Headers.CacheControl?.NoStore == true,
            "Authorized attachment download changed content or lacked safe headers.");
        using var foreignDelete = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{root}/{session}/materials/{id}", 404, bearer: mentor);
        using var ownDelete = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{root}/{session}/materials/{id}", 204, bearer: mentee);
        using var removed = await DatabaseChecks.SendAsync(client, HttpMethod.Get, url, 404, bearer: mentee);
    }

    private static async Task DrainAsync(IServiceProvider services)
    {
        for (var index = 0; index < 500; index++)
        {
            await using var scope = services.CreateAsyncScope();
            if (!await scope.ServiceProvider.GetRequiredService<BookingLifecycleRelay>().DispatchOneAsync(default)) return;
        }
        throw new InvalidOperationException("Booking relay failed to drain its bounded test backlog.");
    }

    private static async Task CheckRelayRecoveryAsync(IServiceProvider services, Guid bookingId)
    {
        var id = Guid.NewGuid();
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            db.OutboxMessages.Add(new OutboxMessage { Id = id, Type = "Booking.Completed.v1", Content = JsonSerializer.Serialize(new { Id = bookingId }), OccurredOnUtc = Clock.GetUtcNow() });
            await db.SaveChangesAsync();
        }
        await using (var scope = services.CreateAsyncScope())
        {
            IEnumerable<IBookingLifecycleConsumer> fault = [new FailAfterDelivery(scope.ServiceProvider.GetServices<IBookingLifecycleConsumer>().ToArray())];
            var relay = ActivatorUtilities.CreateInstance<BookingLifecycleRelay>(scope.ServiceProvider, fault, NullLogger<BookingLifecycleRelay>.Instance);
            Program.Check(await relay.DispatchOneAsync(default), "Fault relay did not select the test event.");
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var row = await db.OutboxMessages.AsNoTracking().SingleAsync(x => x.Id == id);
            Program.Check(row.ProcessedOnUtc is null && row.Error is not null, "Failed acknowledgement lost its pending outbox record.");
            Program.Check(await scope.ServiceProvider.GetRequiredService<LearningDbContext>().Inbox.AnyAsync(x => x.Id == id), "Consumer commit was lost before acknowledgement.");
        }
        Clock.Now = Clock.GetUtcNow().AddSeconds(31);
        await DrainAsync(services);
        await using var check = services.CreateAsyncScope();
        Program.Check(await check.ServiceProvider.GetRequiredService<BookingDbContext>().OutboxMessages.AnyAsync(x => x.Id == id && x.ProcessedOnUtc != null), "Outbox retry did not acknowledge the already committed inbox receipt.");
    }

    private sealed class FailAfterDelivery(IReadOnlyList<IBookingLifecycleConsumer> consumers) : IBookingLifecycleConsumer
    {
        public async Task<Result> ConsumeAsync(Guid eventId, Guid bookingId, CancellationToken ct)
        {
            foreach (var consumer in consumers)
            {
                var result = await consumer.ConsumeAsync(eventId, bookingId, ct);
                if (result.IsFailure) return result;
            }
            throw new IOException("Simulated interruption after inbox commit.");
        }
    }
    private sealed class AdjustableClock : TimeProvider
    {
        public DateTimeOffset? Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now ?? DateTimeOffset.UtcNow;
    }
}
