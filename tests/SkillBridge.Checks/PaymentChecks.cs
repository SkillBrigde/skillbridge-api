using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Infrastructure;
using SkillBridge.Modules.Payments.Application;
using SkillBridge.Modules.Payments.Domain;
using SkillBridge.Modules.Payments.Infrastructure;

namespace SkillBridge.Checks;

internal static class PaymentChecks
{
    private const string Checksum = "Test-only-PayOS-checksum-never-use-in-production";
    private const string Secret = "Test-only-VnPay-secret-never-use-in-production";
    private const string Return = "https://checks.test/result";
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPaymentGateway>(_ => new PaymentGateway(new HttpClient(new PayOsHandler()), configuration));
    }
    public static void Configure(IConfigurationBuilder configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Payments:VnPay:Enabled"] = "true",
        ["Payments:VnPay:TmnCode"] = "CHECKS01",
        ["Payments:VnPay:HashSecret"] = Secret,
        ["Payments:PayOS:Enabled"] = "true",
        ["Payments:PayOS:ClientId"] = "checks-client",
        ["Payments:PayOS:ApiKey"] = "checks-api",
        ["Payments:PayOS:ChecksumKey"] = Checksum,
        ["Payments:AllowedReturnUrls:0"] = Return
    });
    public static void CheckDomain()
    {
        var now = DateTimeOffset.UtcNow;
        Program.Check(PaymentGateway.Sign256(new string('\x0b', 20), "Hi There") == "b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7",
            "HMAC SHA256 failed RFC 4231 vector.");
        Program.Check(PaymentGateway.VnPayCanonical(new Dictionary<string, string> { ["vnp_z"] = "x+y", ["vnp_a"] = "a b" }) == "vnp_a=a+b&vnp_z=x%2By", "VNPAY canonicalization changed.");
        var order = PaymentOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500000, "VND", "VnPay", "key", "hash", Return, Return, now.AddMinutes(10), now).Value;
        Program.Check(order.Verify("reference", 499999, "VND", now).IsFailure && order.GatewayReference is null, "Wrong amount changed payment.");
        Program.Check(order.Verify("reference", 500000, "VND", now).IsSuccess && order.Verify("reference", 500000, "VND", now).IsSuccess &&
            order.Verify("different", 500000, "VND", now).IsFailure, "Payment verification is not idempotent.");
        Program.Check(LedgerEntry.Receive(order, now).Sum(x => x.Amount) == 0, "Payment ledger is unbalanced.");
        Program.Check(PaymentsService.Withdraw(new(1, "VCB", "123456789", "NGUYEN VAN A")).Error == DisbursementPolicy.Locked, "Disbursement was unlocked.");
        using var nested = JsonDocument.Parse("{\"transactions\":[{\"z\":2,\"a\":\"đ\"}],\"amount\":1}");
        Program.Check(PaymentGateway.TryPayOsCanonical(nested.RootElement, out var canonical) && canonical == "amount=1&transactions=[{\"a\":\"đ\",\"z\":2}]", "PayOS nested signature canonicalization differs from SDK.");
        var config = new ConfigurationBuilder(); Configure(config);
        var gateway = new PaymentGateway(new HttpClient(), config.Build());
        Program.Check(gateway.VerifyPayOs("[]").IsFailure && gateway.VerifyPayOs("{\"data\":{\"orderCode\":\"bad\"},\"signature\":\"x\"}").IsFailure, "Malformed webhook accepted.");
        Program.Check(!gateway.IsAllowedReturnUrl(Return + "?next=evil") && !gateway.IsAllowedReturnUrl("https://evil.test/result"), "Return URL allowlist bypassed.");
    }
    public static async Task RunAsync(HttpClient client, IServiceProvider services, string token)
    {
        var mentor = await MessagingChecks.SeedAsync(services, "payments-mentor@checks.test", "Mentor");
        var stranger = await MessagingChecks.SeedAsync(services, "payments-stranger@checks.test", "Mentee");
        using var profile = await DatabaseChecks.SendAsync(client, HttpMethod.Put, "/api/v1/profiles/mentors/me", 204,
            new { headline = "Payments Mentor", bio = "Mentor", hourlyRate = 500000, currency = "VND", timezone = "UTC", isAcceptingMentees = true }, mentor.Token);
        using var profileRead = await DatabaseChecks.SendAsync(client, HttpMethod.Get, "/api/v1/profiles/mentors/me", 200, bearer: mentor.Token);
        var mentorId = (await DatabaseChecks.JsonAsync(profileRead)).GetProperty("id").GetGuid();
        using var settings = await DatabaseChecks.SendAsync(client, HttpMethod.Put, "/api/v1/scheduling/settings", 204,
            new { sessionDurationMinutes = 60, bufferTimeMinutes = 0, leadTimeHours = 0, maxDaysInAdvance = 14, timezone = "UTC" }, mentor.Token);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6));
        using var rule = await DatabaseChecks.SendAsync(client, HttpMethod.Post, "/api/v1/scheduling/availability-rules", 201,
            new { dayOfWeek = (int)date.DayOfWeek, startTime = "09:00:00", endTime = "13:00:00" }, mentor.Token);
        using var slotReply = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"/api/v1/scheduling/mentors/{mentorId}/slots?startDate={date:yyyy-MM-dd}&endDate={date:yyyy-MM-dd}", 200);
        var slots = (await DatabaseChecks.JsonAsync(slotReply)).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        using var service = await DatabaseChecks.SendAsync(client, HttpMethod.Post, "/api/v1/catalog/services", 201,
            new
            {
                title = "Payments service",
                description = "Nội dung",
                serviceType = "Tier1_ReviewCV",
                durationMinutes = 60,
                price = 500000,
                currency = "VND",
                totalSessions = 1,
                deliverables = "Tài liệu",
                isActive = true
            }, mentor.Token);
        var serviceJson = await DatabaseChecks.JsonAsync(service);
        var ids = new List<Guid>();
        foreach (var slot in slots.Take(4))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/booking/bookings")
            {
                Content = JsonContent.Create(new
                {
                    serviceId = serviceJson.GetProperty("id").GetGuid(),
                    serviceVersion = serviceJson.GetProperty("version").GetInt64(),
                    slotIds = new[] { slot },
                    intakeForm = new { coreQuestion = "Câu hỏi", attachedLinks = "https://checks.test/cv", targetGoals = "Mục tiêu" }
                })
            };
            request.Headers.Authorization = new("Bearer", token); request.Headers.Add("Idempotency-Key", "payment-booking-" + slot);
            using var created = await client.SendAsync(request); Program.Check((int)created.StatusCode == 201, "Payments booking fixture failed.");
            ids.Add((await DatabaseChecks.JsonAsync(created)).GetProperty("id").GetGuid());
        }
        const string root = "/api/v1/payments";
        using var anonymous = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/wallet", 401);
        using var untrustedReturn = await CreateAsync(ids[0], "bad-url", "VnPay", 400, token, "https://evil.test");
        using var wrongAmount = await CreateAsync(ids[0], "bad-amount", "VnPay", 409, token, amount: 1);
        using var foreign = await CreateAsync(ids[0], "foreign", "VnPay", 404, stranger.Token);
        using var first = await CreateAsync(ids[0], "payment-1", "VnPay", 200, token);
        var firstJson = await DatabaseChecks.JsonAsync(first); var code = firstJson.GetProperty("orderCode").GetInt64();
        var parallel = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => CreateAsync(ids[0], "payment-1", "VnPay", 200, token)));
        foreach (var response in parallel) { Program.Check((await DatabaseChecks.JsonAsync(response)).GetProperty("orderCode").GetInt64() == code, "Concurrent create duplicated order."); response.Dispose(); }
        using var changed = await CreateAsync(ids[0], "payment-1", "PayOS", 409, token);
        var query = VnPayFields(code, 500000, "123456789");
        using var forged = await client.GetAsync(root + "/webhooks/vnpay?" + Query(query, "invalid"));
        Program.Check((await DatabaseChecks.JsonAsync(forged)).GetProperty("RspCode").GetString() == "97", "Forged VNPAY accepted.");
        var callbackUrl = root + "/webhooks/vnpay?" + Query(query, Sign512(PaymentGateway.VnPayCanonical(query)));
        var callbacks = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.GetAsync(callbackUrl)));
        foreach (var response in callbacks) { Program.Check((await DatabaseChecks.JsonAsync(response)).GetProperty("RspCode").GetString() == "00", "Duplicate VNPAY acknowledgement failed."); response.Dispose(); }
        using var paid = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"/api/v1/booking/bookings/{ids[0]}", 200, bearer: token);
        Program.Check((await DatabaseChecks.JsonAsync(paid)).GetProperty("status").GetString() == "AwaitingMentor", "Verified gateway did not reach Booking.");
        using var escrow = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/bookings/{ids[0]}/escrow", 200, bearer: mentor.Token);
        Program.Check((await DatabaseChecks.JsonAsync(escrow)).GetProperty("platformFeeAmount").ValueKind == JsonValueKind.Null, "Invented platform fee.");
        using var privateEscrow = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/bookings/{ids[0]}/escrow", 404, bearer: stranger.Token);
        using var cancel = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"/api/v1/booking/bookings/{ids[0]}/cancel", 204, new { reason = "Kiểm tra giữ tiền" }, token);
        await using (var scope = services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<PaymentsService>().SyncBookingAsync(ids[0], default);
        using var refund = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/bookings/{ids[0]}/escrow", 200, bearer: token);
        Program.Check((await DatabaseChecks.JsonAsync(refund)).GetProperty("status").GetString() == "ReviewRequired", "Cancellation released money without policy.");
        using var late = await CreateAsync(ids[1], "payment-late", "VnPay", 200, token);
        var lateCode = (await DatabaseChecks.JsonAsync(late)).GetProperty("orderCode").GetInt64();
        using var lateCancel = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"/api/v1/booking/bookings/{ids[1]}/cancel", 204, new { reason = "Đóng trước callback" }, token);
        var lateFields = VnPayFields(lateCode, 500000, "223456789");
        using var lateCallback = await client.GetAsync(root + "/webhooks/vnpay?" + Query(lateFields, Sign512(PaymentGateway.VnPayCanonical(lateFields))));
        using var lateEscrow = await DatabaseChecks.SendAsync(client, HttpMethod.Get, $"{root}/bookings/{ids[1]}/escrow", 200, bearer: token);
        Program.Check((await DatabaseChecks.JsonAsync(lateEscrow)).GetProperty("status").GetString() == "ReviewRequired", "Late funds were lost or booked.");
        using var payos = await CreateAsync(ids[2], "payment-payos", "PayOS", 200, token);
        var payosCode = (await DatabaseChecks.JsonAsync(payos)).GetProperty("orderCode").GetInt64();
        var data = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["amount"] = 500000,
            ["code"] = "00",
            ["currency"] = "VND",
            ["orderCode"] = payosCode,
            ["reference"] = "PAYOS-CHECKS-1"
        };
        using var invalidPayos = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/webhooks/payos", 401, new { data, signature = "wrong" });
        using var validPayos = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/webhooks/payos", 200, new { data, signature = SignData(data) });
        using var retryPayos = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/webhooks/payos", 200, new { data, signature = SignData(data) });
        var handshake = new SortedDictionary<string, object>(StringComparer.Ordinal) { ["amount"] = 3000, ["code"] = "00", ["currency"] = "VND", ["orderCode"] = 123, ["reference"] = "PAYOS-WEBHOOK-PROBE" };
        using var probe = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/webhooks/payos", 200, new { data = handshake, signature = SignData(handshake) });
        data["amount"] = 400000;
        using var mismatch = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/webhooks/payos", 409, new { data, signature = SignData(data) });
        using var recovery = await CreateAsync(ids[3], "payment-recovery", "VnPay", 200, token);
        var recoveryJson = await DatabaseChecks.JsonAsync(recovery);
        var recoveryId = recoveryJson.GetProperty("paymentId").GetGuid();
        await using (var scope = services.CreateAsyncScope())
        {
            var interrupted = ActivatorUtilities.CreateInstance<PaymentsService>(scope.ServiceProvider,
                new CrashAfterApply(scope.ServiceProvider.GetRequiredService<IPaymentBookings>()));
            try
            {
                await interrupted.ReceiveAsync("VnPay", new(recoveryJson.GetProperty("orderCode").GetInt64(), 500000, "VND", "323456789", true), default);
                throw new InvalidOperationException("Payment fault was not injected.");
            }
            catch (IOException) { }
        }
        await using (var scope = services.CreateAsyncScope())
        {
            Program.Check(await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Orders.AnyAsync(x => x.Id == recoveryId && x.Status == "Verified"), "Payment intent was not durable after Booking commit.");
            Program.Check((await scope.ServiceProvider.GetRequiredService<PaymentsService>().ApplyAsync(recoveryId, default)).IsSuccess, "Payment retry after Booking commit failed.");
        }
        using var wallet = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/wallet", 200, bearer: token);
        Program.Check((await DatabaseChecks.JsonAsync(wallet)).GetProperty("balance").GetDecimal() == 0, "Escrow incorrectly became spendable wallet credit.");
        using var transactions = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/wallet/transactions", 200, bearer: token);
        using var badPage = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/wallet/transactions?page=0", 400, bearer: token);
        using var locked = await DatabaseChecks.SendAsync(client, HttpMethod.Post, root + "/withdraw", 409,
            new { amount = 1, bankCode = "VCB", accountNumber = "123456789", accountName = "NGUYEN VAN A" }, mentor.Token);
        using var payouts = await DatabaseChecks.SendAsync(client, HttpMethod.Get, root + "/payouts", 200, bearer: mentor.Token);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            Program.Check(await db.Orders.CountAsync() == 4 && await db.Escrows.CountAsync() == 4 && await db.Ledger.CountAsync() == 8 && await db.Ledger.SumAsync(x => x.Amount) == 0,
                "Callback races duplicated funds or broke ledger balance.");
        }
        Console.WriteLine("PASS: Payments signatures, gateway protocols, ownership, idempotent races, balanced ledger, late funds and locked disbursement.");

        async Task<HttpResponseMessage> CreateAsync(Guid booking, string key, string gateway, int expected, string bearer, string url = Return, decimal? amount = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, root + "/create-payment") { Content = JsonContent.Create(new { bookingId = booking, gateway, returnUrl = url, cancelUrl = Return, amount }) };
            request.Headers.Authorization = new("Bearer", bearer); request.Headers.Add("Idempotency-Key", key);
            var response = await client.SendAsync(request);
            Program.Check((int)response.StatusCode == expected, $"Create-payment expected {expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            return response;
        }
    }
    private static Dictionary<string, string> VnPayFields(long code, decimal amount, string reference) => new()
    {
        ["vnp_TmnCode"] = "CHECKS01",
        ["vnp_TxnRef"] = code.ToString(CultureInfo.InvariantCulture),
        ["vnp_Amount"] = (amount * 100).ToString("0", CultureInfo.InvariantCulture),
        ["vnp_TransactionNo"] = reference,
        ["vnp_ResponseCode"] = "00",
        ["vnp_TransactionStatus"] = "00"
    };
    private static string Query(Dictionary<string, string> fields, string signature) => string.Join("&", fields.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value))) + "&vnp_SecureHash=" + signature;
    private static string Sign512(string value) => Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(value)));
    private static string SignData(SortedDictionary<string, object> data) => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Checksum),
        Encoding.UTF8.GetBytes(string.Join("&", data.Select(x => x.Key + "=" + Convert.ToString(x.Value, CultureInfo.InvariantCulture))))));
    private sealed class CrashAfterApply(IPaymentBookings inner) : IPaymentBookings
    {
        public Task<Result<PaymentBooking>> GetAsync(Guid id, CancellationToken ct) => inner.GetAsync(id, ct);
        public async Task<Result> ApplyAsync(Guid bookingId, Guid paymentId, decimal amount, string currency, CancellationToken ct)
        {
            var result = await inner.ApplyAsync(bookingId, paymentId, amount, currency, ct);
            if (result.IsSuccess) throw new IOException("Interrupted after Booking committed.");
            return result;
        }
    }
    private sealed class PayOsHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Get) return new(HttpStatusCode.NotFound);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var data = new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["orderCode"] = body.RootElement.GetProperty("orderCode").GetInt64(),
                ["amount"] = body.RootElement.GetProperty("amount").GetInt64(),
                ["currency"] = "VND",
                ["checkoutUrl"] = "https://pay.payos.vn/web/checks"
            };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { code = "00", data, signature = SignData(data) }) };
        }
    }
}
