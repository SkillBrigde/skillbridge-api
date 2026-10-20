using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Application;
using SkillBridge.Modules.Booking.Application.DTOs;
using SkillBridge.Modules.Booking.Domain;
using SkillBridge.Modules.Booking.Infrastructure;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Checks;

internal static class BookingChecks
{
    public static void CheckDomain()
    {
        var now = DateTimeOffset.UtcNow;
        var terms = new ServiceTerms(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Gói hai buổi", "Mô tả",
            "Tier3_Roadmap", "Tài liệu", 500000, "VND", 60, 2, now);
        var intake = new IntakeForm("Câu hỏi", "https://example.com/cv", "Mục tiêu");
        var slots = new[] { Guid.NewGuid(), Guid.NewGuid() };
        Program.Check(BookingOrder.Create(Guid.NewGuid(), terms, intake, [slots[0]], "key", new('A', 64), now).IsFailure,
            "Package accepted too few sessions.");
        Program.Check((intake with { AttachedLinks = "javascript:alert(1)" }).Validate().IsFailure, "Unsafe intake link accepted.");
        var order = BookingOrder.Create(Guid.NewGuid(), terms, intake, slots, "key", new('A', 64), now).Value;
        Program.Check(order.Confirm(terms.MentorUserId, null, now).IsFailure, "Unpaid booking was confirmed.");
        Program.Check(order.FinishHold([new(slots[0], now.AddDays(3), now.AddDays(3).AddHours(1)),
            new(slots[1], now.AddDays(4), now.AddDays(4).AddHours(1))], order.HeldUntilUtc, now).IsSuccess, "Valid multi-session hold rejected.");
        Program.Check(order.ReceivePayment(Guid.NewGuid(), 1, "VND", now).IsFailure && order.PaymentId is null,
            "Incorrect payment mutated booking.");
        Program.Check(order.ReceivePayment(Guid.NewGuid(), terms.Price, terms.Currency, now).IsSuccess &&
            order.Confirm(terms.MentorUserId, null, now).IsSuccess, "Trusted payment/confirmation failed.");
        Program.Check(order.Complete(order.MenteeUserId, [order.Sessions[0].Id], now.AddDays(5)).IsFailure, "Incomplete package marked completed.");
        Program.Check(order.Cancel(order.MenteeUserId, "Không học nữa", now).IsSuccess && order.Status == "RefundReview",
            "Paid cancellation must await policy review rather than fabricate a refund.");
    }

    public static async Task RunAsync(HttpClient client, IServiceProvider services, string menteeToken)
    {
        await using (var scope = services.CreateAsyncScope())
            Program.Check((await scope.ServiceProvider.GetRequiredService<IBookingSlotReservations>()
                .ReleaseAsync(Guid.NewGuid(), Guid.NewGuid(), default)).IsSuccess, "Cleanup without acquired slots must not require scheduling settings.");
        var mentor = await MessagingChecks.SeedAsync(services, "booking-mentor@checks.test", "Mentor");
        var stranger = await MessagingChecks.SeedAsync(services, "booking-stranger@checks.test", "Mentee");
        const string scheduling = "/api/v1/scheduling";
        const string root = "/api/v1/booking/bookings";
        using var profile = await DatabaseChecks.SendAsync(client, HttpMethod.Put, "/api/v1/profiles/mentors/me", 204,
            new { headline = "Booking Mentor", bio = "Mentor", hourlyRate = 500000, currency = "VND", timezone = "UTC", isAcceptingMentees = true }, mentor.Token);
        using var profileRead = await DatabaseChecks.SendAsync(client, HttpMethod.Get, "/api/v1/profiles/mentors/me", 200, bearer: mentor.Token);
        var mentorId = (await DatabaseChecks.JsonAsync(profileRead)).GetProperty("id").GetGuid();
        using var settings = await DatabaseChecks.SendAsync(client, HttpMethod.Put, scheduling + "/settings", 204,
            new { sessionDurationMinutes = 60, bufferTimeMinutes = 0, leadTimeHours = 0, maxDaysInAdvance = 14, timezone = "UTC" }, mentor.Token);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4));
        using var rule = await DatabaseChecks.SendAsync(client, HttpMethod.Post, scheduling + "/availability-rules", 201,
            new { dayOfWeek = (int)date.DayOfWeek, startTime = "09:00:00", endTime = "13:00:00" }, mentor.Token);
        using var listing = await DatabaseChecks.SendAsync(client, HttpMethod.Get,
            $"{scheduling}/mentors/{mentorId}/slots?startDate={date:yyyy-MM-dd}&endDate={date:yyyy-MM-dd}", 200);
        var slotIds = (await DatabaseChecks.JsonAsync(listing)).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Program.Check(slotIds.Length == 4, "Booking fixture must contain four slots.");
        var serviceBody = new
        {
            title = "Gói hai buổi",
            description = "Lộ trình",
            serviceType = "Tier3_Roadmap",
            durationMinutes = 60,
            price = 500000,
            currency = "VND",
            totalSessions = 2,
            deliverables = "Tài liệu",
            isActive = true
        };
        using var serviceReply = await DatabaseChecks.SendAsync(client, HttpMethod.Post, "/api/v1/catalog/services", 201, serviceBody, mentor.Token);
        var serviceJson = await DatabaseChecks.JsonAsync(serviceReply);
        var serviceId = serviceJson.GetProperty("id").GetGuid();
        var version = serviceJson.GetProperty("version").GetInt64();
        var request = new CreateBookingRequest(serviceId, version, slotIds[..2], new("Câu hỏi", "https://example.com/cv", "Mục tiêu"));
        async Task<HttpResponseMessage> Create(string? key, CreateBookingRequest body, int? expected, string? token = null)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, root) { Content = JsonContent.Create(body) };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? menteeToken);
            if (key is not null) message.Headers.Add("Idempotency-Key", key);
            var response = await client.SendAsync(message);
            if (expected.HasValue && (int)response.StatusCode != expected)
                throw new InvalidOperationException($"Booking create expected {expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            return response;
        }
        using var noAuth = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root, 401);
        using var noKey = await Create(null, request, 400);
        using var wrongRole = await Create("wrong-role", request, 403, mentor.Token);
        using var notEnough = await Create("few-slots", request with { SlotIds = slotIds[..1] }, 400);
        using var changed = await Create("wrong-version", request with { ServiceVersion = version + 1 }, 409);
        var concurrent = await Task.WhenAll(Create("same-key", request, 201), Create("same-key", request, 201));
        Guid bookingId;
        JsonElement booking;
        try
        {
            booking = await DatabaseChecks.JsonAsync(concurrent[0]);
            bookingId = booking.GetProperty("id").GetGuid();
            Program.Check((await DatabaseChecks.JsonAsync(concurrent[1])).GetProperty("id").GetGuid() == bookingId,
                "Concurrent idempotent creation produced multiple bookings.");
        }
        finally { foreach (var response in concurrent) response.Dispose(); }
        using var keyMismatch = await Create("same-key", request with { SlotIds = slotIds[2..] }, 409);
        using var foreign = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/{bookingId}", 404, bearer: stranger.Token);
        using var foreignAudit = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/{bookingId}/audit-logs", 404, bearer: stranger.Token);
        using var unpaidConfirm = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{bookingId}/confirm", 409, new { mentorNote = "" }, mentor.Token);
        using var badPage = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "?pageSize=101", 400, bearer: menteeToken);
        var race = await Task.WhenAll(Create("race-a", request with { SlotIds = slotIds[2..] }, null),
            Create("race-b", request with { SlotIds = slotIds[2..] }, null));
        try
        {
            Program.Check(race.Count(x => (int)x.StatusCode == 201) == 1 && race.Count(x => (int)x.StatusCode == 409) == 1,
                "Two package bookings reserved identical slots.");
            var raceId = (await DatabaseChecks.JsonAsync(race.Single(x => (int)x.StatusCode == 201))).GetProperty("id").GetGuid();
            using var cancelRace = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{raceId}/cancel", 204, new { reason = "Hủy để kiểm tra" }, menteeToken);
        }
        finally { foreach (var response in race) response.Dispose(); }
        var paymentId = Guid.NewGuid();
        await using (var scope = services.CreateAsyncScope())
        {
            var bookings = scope.ServiceProvider.GetRequiredService<Bookings>();
            Program.Check((await bookings.ReceivePaymentAsync(bookingId, paymentId, 1, "VND", default)).IsFailure, "Booking trusted the wrong payment amount.");
            Program.Check((await bookings.ReceivePaymentAsync(bookingId, paymentId, 500000, "VND", default)).IsSuccess, "Trusted payment did not book both slots.");
            Program.Check((await bookings.ReceivePaymentAsync(bookingId, paymentId, 500000, "VND", default)).IsSuccess, "Payment retry is not idempotent.");
            Program.Check((await bookings.ReceivePaymentAsync(bookingId, paymentId, 1, "VND", default)).IsFailure, "Payment retry accepted a changed amount.");
        }
        using var confirm = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{bookingId}/confirm", 204, new { mentorNote = "Chuẩn bị tài liệu" }, mentor.Token);
        using var confirmAgain = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{bookingId}/confirm", 204, new { mentorNote = "Chuẩn bị tài liệu" }, mentor.Token);
        using var unavailableEvidence = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{bookingId}/complete", 503, bearer: menteeToken);
        var firstSession = booking.GetProperty("sessions")[0].GetProperty("id").GetGuid();
        using var reschedule = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{bookingId}/reschedule", 201,
            new { bookingSessionId = firstSession, newSlotId = slotIds[2], reason = "Đổi giờ" }, menteeToken);
        var requestId = (await DatabaseChecks.JsonAsync(reschedule)).GetProperty("id").GetGuid();
        var respondUrl = $"/api/v1/booking/reschedule-requests/{requestId}/respond";
        using var selfApprove = await DatabaseChecks.SendAsync(client, HttpMethod.Post, respondUrl, 403, new { isApproved = true }, menteeToken);
        using var strangerApprove = await DatabaseChecks.SendAsync(client, HttpMethod.Post, respondUrl, 404, new { isApproved = true }, stranger.Token);
        using var approved = await DatabaseChecks.SendAsync(client, HttpMethod.Post, respondUrl, 204, new { isApproved = true }, mentor.Token);
        using var approvedAgain = await DatabaseChecks.SendAsync(client, HttpMethod.Post, respondUrl, 204, new { isApproved = true }, mentor.Token);
        using var detail = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/{bookingId}", 200, bearer: menteeToken);
        var moved = (await DatabaseChecks.JsonAsync(detail)).GetProperty("sessions").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == firstSession);
        Program.Check(moved.GetProperty("slotId").GetGuid() == slotIds[2], "Approved reschedule did not retain the session ID and change its slot.");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            Program.Check(await db.OutboxMessages.CountAsync(x => x.Type == "Booking.Confirmed.v1") == 1 &&
                await db.OutboxMessages.CountAsync(x => x.Type == "Booking.Rescheduled.v1") == 1, "Retries duplicated lifecycle events.");
            var slots = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            Program.Check(await slots.Slots.CountAsync(x => x.BookingId == bookingId && x.Status == "Booked") == 2 &&
                await slots.Slots.AnyAsync(x => x.Id == slotIds[0] && x.Status == "Available"), "Reschedule failed to swap only one booked slot.");
        }
        using var interruptedMove = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{bookingId}/reschedule", 201,
            new { bookingSessionId = firstSession, newSlotId = slotIds[0], reason = "Kiểm tra phục hồi dời lịch" }, menteeToken);
        var interruptedId = (await DatabaseChecks.JsonAsync(interruptedMove)).GetProperty("id").GetGuid();
        await using (var scope = services.CreateAsyncScope())
        {
            var fault = new InterruptedReservations(scope.ServiceProvider.GetRequiredService<IBookingSlotReservations>(), false);
            var reschedules = ActivatorUtilities.CreateInstance<Reschedules>(scope.ServiceProvider, fault);
            var interrupted = false;
            try { await reschedules.RespondAsync(interruptedId, mentor.Id, new(true, null), default); }
            catch (IOException) { interrupted = true; }
            Program.Check(interrupted, "Fault injection failed to interrupt after the Scheduling swap committed.");
        }
        using var cannotCancelDuringMove = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{bookingId}/cancel", 409, new { reason = "Chưa xong dời lịch" }, mentor.Token);
        using var recoveredMove = await DatabaseChecks.SendAsync(client, HttpMethod.Post,
            $"/api/v1/booking/reschedule-requests/{interruptedId}/respond", 204, new { isApproved = true }, mentor.Token);
        using var cancelPaid = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{root}/{bookingId}/cancel", 204, new { reason = "Xin hủy gói" }, mentor.Token);
        using var cancelled = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/{bookingId}", 200, bearer: menteeToken);
        Program.Check((await DatabaseChecks.JsonAsync(cancelled)).GetProperty("status").GetString() == "RefundReview", "Paid cancellation fabricated a completed refund.");
        using var expiring = await Create("expiring", request, 201);
        var expiringId = (await DatabaseChecks.JsonAsync(expiring)).GetProperty("id").GetGuid();
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var provider = new FixedTimeProvider(DateTimeOffset.UtcNow.AddMinutes(11));
            var bookings = ActivatorUtilities.CreateInstance<Bookings>(scope.ServiceProvider, provider);
            await bookings.ReconcileAsync(expiringId, default);
            Program.Check(await db.Bookings.AnyAsync(x => x.Id == expiringId && x.Status == "Cancelled" && !x.NeedsSlotRelease), "Expired booking failed to release its slots.");
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var fault = new InterruptedReservations(scope.ServiceProvider.GetRequiredService<IBookingSlotReservations>(), true);
            var bookings = ActivatorUtilities.CreateInstance<Bookings>(scope.ServiceProvider, fault);
            var interrupted = false;
            try { await bookings.CreateAsync(booking.GetProperty("menteeUserId").GetGuid(), "interrupted-hold", request, default); }
            catch (IOException) { interrupted = true; }
            Program.Check(interrupted, "Fault injection failed to interrupt after the Scheduling hold committed.");
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var intent = await db.Bookings.AsNoTracking().SingleAsync(x => x.IdempotencyKey == "interrupted-hold");
            Program.Check(intent.Status == "Holding", "Interrupted creation lost its persisted intent.");
            var bookings = scope.ServiceProvider.GetRequiredService<Bookings>();
            Program.Check((await bookings.ResumeHoldAsync(intent.Id, default)).IsSuccess, "Unable to reconcile committed slots after creation interruption.");
            Program.Check(await db.Bookings.AnyAsync(x => x.Id == intent.Id && x.Status == "PendingPayment"), "Recovered package did not reach PendingPayment.");
            Program.Check((await bookings.CancelAsync(intent.Id, intent.MenteeUserId, "Kết thúc kiểm tra", default)).IsSuccess, "Recovered package cannot be cancelled.");
        }
        Console.WriteLine("PASS: Booking package creation, ownership, idempotency races, trusted payment, reschedule swap, refund review and expiry recovery.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InterruptedReservations(IBookingSlotReservations inner, bool interruptHold) : IBookingSlotReservations
    {
        public async Task<Result<HeldPackage>> HoldAsync(Guid mentorId, Guid bookingId, IReadOnlyList<Guid> slots, int duration, DateTimeOffset until, CancellationToken ct)
        {
            var result = await inner.HoldAsync(mentorId, bookingId, slots, duration, until, ct);
            if (interruptHold && result.IsSuccess) throw new IOException("Simulated interruption after durable slot hold.");
            return result;
        }
        public Task<Result> ConfirmAsync(Guid mentorId, Guid bookingId, int count, CancellationToken ct) => inner.ConfirmAsync(mentorId, bookingId, count, ct);
        public Task<Result> ReleaseAsync(Guid mentorId, Guid bookingId, CancellationToken ct) => inner.ReleaseAsync(mentorId, bookingId, ct);
        public async Task<Result<SessionWindow>> MoveAsync(Guid mentorId, Guid bookingId, Guid oldSlot, Guid newSlot, int duration, CancellationToken ct)
        {
            var result = await inner.MoveAsync(mentorId, bookingId, oldSlot, newSlot, duration, ct);
            if (!interruptHold && result.IsSuccess) throw new IOException("Simulated interruption after durable slot swap.");
            return result;
        }
    }
}
