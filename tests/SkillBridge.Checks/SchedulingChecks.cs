using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Modules.Scheduling.Application;
using SkillBridge.Modules.Scheduling.Domain;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Checks;

internal static class SchedulingChecks
{
    public static void CheckDomain()
    {
        var mentor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        SchedulingSettings Settings(int duration, string zone) => SchedulingSettings.Create(mentor,
            SchedulingSettingsValues.Create(duration, 0, 0, 1, zone).Value, now).Value;
        AvailabilityRule Rule(int day, int start, int end) => AvailabilityRule.Create(mentor,
            AvailabilityWindow.Create(day, new(start, 0), new(end, 0)).Value, now).Value;
        var spring = SlotGenerator.Generate(Settings(30, "America/New_York"), [Rule(0, 1, 4)], new(2026, 3, 8));
        Program.Check(spring.Count == 3 && spring.All(x => x.End - x.Start == TimeSpan.FromMinutes(30)),
            "DST spring gap created a non-existent or wrong-duration slot.");
        var autumn = SlotGenerator.Generate(Settings(30, "America/New_York"), [Rule(0, 1, 2)], new(2026, 11, 1));
        Program.Check(autumn.Count == 1 && autumn[0].Start == DateTimeOffset.Parse("2026-11-01T05:00:00Z", CultureInfo.InvariantCulture),
            "DST repeated hour must select the earlier occurrence exactly once.");
        Program.Check(TimeOff.Create(mentor, new(2026, 1, 2), new(2026, 1, 1), null, now).IsFailure,
            "Time-off accepted reversed dates.");
        var slot = AvailabilitySlot.Create(mentor, now.AddDays(1), now.AddDays(1).AddHours(1), now);
        var booking = Guid.NewGuid();
        Program.Check(slot.Hold(booking, now.AddMinutes(10), now).IsSuccess && slot.Hold(Guid.NewGuid(), now.AddMinutes(10), now).IsFailure,
            "Another booking stole a protected slot.");
        Program.Check(slot.Confirm(booking, now.AddMinutes(11)).IsFailure, "Expired slot hold was confirmed.");
    }

    public static async Task RunAsync(HttpClient client, IServiceProvider services, string menteeToken)
    {
        var owner = await MessagingChecks.SeedAsync(services, "scheduling-owner@checks.test", "Mentor");
        var stranger = await MessagingChecks.SeedAsync(services, "scheduling-other@checks.test", "Mentor");
        const string root = "/api/v1/scheduling";
        using var anonymous = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/time-offs", 401);
        using var denied = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/time-offs", 403, bearer: menteeToken);
        using var missing = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/time-offs", 409,
            new { startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }, owner.Token);
        foreach (var token in new[] { owner.Token, stranger.Token })
        {
            using var profile = await DatabaseChecks.SendAsync(client, HttpMethod.Put, "/api/v1/profiles/mentors/me", 204,
                new { headline = "Scheduling Mentor", bio = "Test", hourlyRate = 100000, currency = "VND", timezone = "UTC", isAcceptingMentees = true }, token);
            using var settings = await DatabaseChecks.SendAsync(client, HttpMethod.Put, root + "/settings", 204,
                new { sessionDurationMinutes = 60, bufferTimeMinutes = 15, leadTimeHours = 0, maxDaysInAdvance = 14, timezone = "UTC" }, token);
        }
        using var mentorRead = await DatabaseChecks.SendAsync(client, HttpMethod.Get, "/api/v1/profiles/mentors/me", 200, bearer: owner.Token);
        var mentor = (await DatabaseChecks.JsonAsync(mentorRead)).GetProperty("id").GetGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var rulePayload = new { dayOfWeek = (int)date.DayOfWeek, startTime = "09:00:00", endTime = "12:00:00" };
        using var rule = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/availability-rules", 201, rulePayload, owner.Token);
        var ruleId = (await DatabaseChecks.JsonAsync(rule)).GetProperty("id").GetGuid();
        var url = $"{root}/mentors/{mentor}/slots?startDate={date:yyyy-MM-dd}&endDate={date:yyyy-MM-dd}";
        using var available = await DatabaseChecks.SendAsync(client, HttpMethod.Get, url, 200);
        var slots = (await DatabaseChecks.JsonAsync(available)).GetProperty("items");
        Program.Check(slots.GetArrayLength() == 2, "60 minute sessions plus 15 minute buffer must produce two slots in 3 hours.");
        var firstSlot = slots[0].GetProperty("id").GetGuid();
        using var repeated = await DatabaseChecks.SendAsync(client, HttpMethod.Get, url, 200);
        Program.Check((await DatabaseChecks.JsonAsync(repeated)).GetProperty("items")[0].GetProperty("id").GetGuid() == firstSlot,
            "Regeneration changed a stable slot ID.");
        using var invalid = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/time-offs", 400,
            new { startDate = date, endDate = date.AddDays(-1) }, owner.Token);
        using var page = await DatabaseChecks.SendAsync(client, HttpMethod.Get, url + "&pageSize=101", 400);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => DatabaseChecks.SendAsync(client,
            HttpMethod.Post, root + "/time-offs", null, new { startDate = date, endDate = date, reason = "Nghỉ" }, owner.Token)));
        Guid timeOff;
        try
        {
            Program.Check(concurrent.Count(x => (int)x.StatusCode == 201) == 1 && concurrent.Count(x => (int)x.StatusCode == 409) == 1,
                "Concurrent overlapping time-offs must have exactly one winner.");
            timeOff = (await DatabaseChecks.JsonAsync(concurrent.Single(x => (int)x.StatusCode == 201))).GetProperty("id").GetGuid();
        }
        finally { foreach (var response in concurrent) response.Dispose(); }
        using var hidden = await DatabaseChecks.SendAsync(client, HttpMethod.Get, url, 200);
        Program.Check((await DatabaseChecks.JsonAsync(hidden)).GetProperty("totalCount").GetInt64() == 0, "Day off left a publicly available slot.");
        using var foreignDelete = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{root}/time-offs/{timeOff}", 404, bearer: stranger.Token);
        using var listing = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/time-offs", 200, bearer: owner.Token);
        Program.Check((await DatabaseChecks.JsonAsync(listing)).GetProperty("totalCount").GetInt64() == 1, "Owner time-off list incorrect.");
        using var removed = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{root}/time-offs/{timeOff}", 204, bearer: owner.Token);
        var ids = slots.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        var bookingA = Guid.NewGuid();
        var bookingB = Guid.NewGuid();
        async Task<bool> Reserve(Guid targetBooking)
        {
            await using var scope = services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<SlotReservations>().HoldAsync(mentor, targetBooking,
                ids, 60, DateTimeOffset.UtcNow.AddMinutes(9), default);
            return result.IsSuccess;
        }
        var reserved = await Task.WhenAll(Reserve(bookingA), Reserve(bookingB));
        Program.Check(reserved.Count(x => x) == 1, "Concurrent packages reserved the same slots twice.");
        var winner = reserved[0] ? bookingA : bookingB;
        await using (var scope = services.CreateAsyncScope())
        {
            var reservations = scope.ServiceProvider.GetRequiredService<SlotReservations>();
            var changed = await reservations.HoldAsync(mentor, winner, [firstSlot], 60, DateTimeOffset.UtcNow.AddMinutes(8), default);
            Program.Check(changed.IsFailure && changed.Error.Code == "Scheduling.ReservationChanged", "Retry silently changed a package slot set.");
            var retry = await reservations.HoldAsync(mentor, winner, ids, 60, DateTimeOffset.UtcNow.AddMinutes(8), default);
            Program.Check(retry.IsSuccess && retry.Value.HeldUntilUtc > DateTimeOffset.UtcNow.AddMinutes(8), "Retry changed the original hold deadline.");
            var result = await reservations.ConfirmAsync(mentor, winner, 2, default);
            Program.Check(result.IsSuccess, "Package confirmation lost its reserved slots.");
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            Program.Check(await db.Slots.CountAsync(x => x.BookingId == winner && x.Status == "Booked") == 2, "Package confirmation was not atomic.");
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<SlotReservations>().ReleaseAsync(mentor, winner, default);
            Program.Check(result.IsSuccess, "Cannot release a package reservation.");
        }
        Guid booking = Guid.NewGuid();
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var slot = await db.Slots.SingleAsync(x => x.Id == firstSlot);
            Program.Check(slot.Status == "Available", "Deleting time-off did not restore the slot.");
            Program.Check(slot.Hold(booking, DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow).IsSuccess, "Test could not hold slot.");
            await db.SaveChangesAsync();
        }
        using var protectedOff = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/time-offs", 409,
            new { startDate = date, endDate = date }, owner.Token);
        using var protectedRule = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{root}/availability-rules/{ruleId}", 409, bearer: owner.Token);
        using var protectedSettings = await DatabaseChecks.SendAsync(client, HttpMethod.Put, root + "/settings", 409,
            new { sessionDurationMinutes = 30, bufferTimeMinutes = 15, leadTimeHours = 0, maxDaysInAdvance = 14, timezone = "UTC" }, owner.Token);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            Program.Check(await db.AvailabilityRules.AnyAsync(x => x.Id == ruleId) && !await db.TimeOffs.AnyAsync(x => x.MentorId == mentor),
                "Rejected schedule changes were not rolled back.");
            var slot = await db.Slots.SingleAsync(x => x.Id == firstSlot);
            slot.Release(booking, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        using var finalRule = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{root}/availability-rules/{ruleId}", 204, bearer: owner.Token);
        using var noSlots = await DatabaseChecks.SendAsync(client, HttpMethod.Get, url, 200);
        Program.Check((await DatabaseChecks.JsonAsync(noSlots)).GetProperty("totalCount").GetInt64() == 0, "Removed rule remained available.");
        Console.WriteLine("PASS: Scheduling slots, DST, buffer, time-off ownership/concurrency, protected holds and transactional regeneration.");
    }
}
