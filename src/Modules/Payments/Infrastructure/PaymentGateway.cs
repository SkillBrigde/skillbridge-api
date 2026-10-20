using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Payments.Application;
using SkillBridge.Modules.Payments.Domain;

namespace SkillBridge.Modules.Payments.Infrastructure;

public sealed class PaymentGateway(HttpClient client, IConfiguration configuration) : IPaymentGateway
{
    private string Setting(string gateway, string key) => configuration[$"Payments:{gateway}:{key}"] ?? "";
    public bool IsConfigured(string gateway) => bool.TryParse(Setting(gateway, "Enabled"), out var enabled) && enabled && (gateway switch
    {
        "PayOS" => new[] { "ClientId", "ApiKey", "ChecksumKey" }.All(x => !string.IsNullOrWhiteSpace(Setting(gateway, x))),
        "VnPay" => new[] { "TmnCode", "HashSecret" }.All(x => !string.IsNullOrWhiteSpace(Setting(gateway, x))),
        _ => false
    });
    public bool IsAllowedReturnUrl(string? url) => url is { Length: <= 2048 } &&
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme == "https" && parsed.UserInfo.Length == 0 &&
        (configuration.GetSection("Payments:AllowedReturnUrls").Get<string[]>() ?? []).Contains(url, StringComparer.Ordinal);

    public async Task<Result<PaymentLink>> CreateAsync(PaymentOrder order, string clientIp, CancellationToken ct)
    {
        if (!IsConfigured(order.Gateway)) return PaymentErrors.Unavailable;
        if (order.Gateway == "VnPay") return CreateVnPay(order, clientIp);
        try
        {
            // Cùng orderCode được dùng lại sau timeout, không tạo mã đơn/cơ hội thu tiền thứ hai.
            using var lookup = Request(HttpMethod.Get, "/v2/payment-requests/" + order.OrderCode);
            using var found = await client.SendAsync(lookup, ct);
            if (found.IsSuccessStatusCode)
            {
                using var existing = JsonDocument.Parse(await found.Content.ReadAsStringAsync(ct));
                if (TryData(existing.RootElement, out var data))
                {
                    var code = data.TryGetProperty("orderCode", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt64(out var v) ? v : 0;
                    if (code != order.OrderCode) return PaymentErrors.Unavailable;
                    // GET không trả checkoutUrl; khôi phục URL bằng paymentLinkId đã xác minh.
                    if (ValidPayOsData(existing.RootElement, out data) && data.TryGetProperty("id", out var id) &&
                        id.ValueKind == JsonValueKind.String && id.GetString() is { Length: > 0 and <= 100 } linkId && linkId.All(char.IsAsciiLetterOrDigit) &&
                        data.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number && a.TryGetDecimal(out var amount) && amount == order.Amount &&
                        String(data, "status") is "PENDING" or "PAID")
                        return new PaymentLink(order.Id, order.OrderCode, order.Gateway, order.Amount, "VND",
                            "https://pay.payos.vn/web/" + linkId, null, null, null, null, order.ExpiresAtUtc);
                    return PaymentErrors.Unavailable;
                }
            }
            else if (found.StatusCode != HttpStatusCode.NotFound && found.StatusCode != HttpStatusCode.BadRequest)
                return PaymentErrors.Unavailable;
            var description = "SB" + order.OrderCode.ToString(CultureInfo.InvariantCulture);
            // payOS giới hạn description 9 ký tự với một số ngân hàng.
            description = description.Length > 9 ? description[^9..] : description;
            var signature = Sign256(Setting("PayOS", "ChecksumKey"), $"amount={order.Amount.ToString("0", CultureInfo.InvariantCulture)}&cancelUrl={order.CancelUrl}&description={description}&orderCode={order.OrderCode}&returnUrl={order.ReturnUrl}");
            using var request = Request(HttpMethod.Post, "/v2/payment-requests");
            request.Content = JsonContent.Create(new
            {
                orderCode = order.OrderCode,
                amount = (long)order.Amount,
                description,
                cancelUrl = order.CancelUrl,
                returnUrl = order.ReturnUrl,
                expiredAt = order.ExpiresAtUtc.ToUnixTimeSeconds(),
                signature
            });
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return PaymentErrors.Unavailable;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!ValidPayOsData(json.RootElement, out var result) ||
                !result.TryGetProperty("orderCode", out var orderCode) || orderCode.ValueKind != JsonValueKind.Number || !orderCode.TryGetInt64(out var actualCode) || actualCode != order.OrderCode ||
                !result.TryGetProperty("amount", out var price) || price.ValueKind != JsonValueKind.Number || !price.TryGetDecimal(out var actualAmount) || actualAmount != order.Amount ||
                String(result, "currency") != "VND" || !Uri.TryCreate(String(result, "checkoutUrl"), UriKind.Absolute, out var url) ||
                url.Scheme != "https" || url.Host != "pay.payos.vn" || url.UserInfo.Length != 0 || !url.IsDefaultPort)
                return PaymentErrors.Unavailable;
            return new PaymentLink(order.Id, order.OrderCode, order.Gateway, order.Amount, "VND", url.AbsoluteUri,
                String(result, "qrCode"), String(result, "accountNumber"), String(result, "accountName"), String(result, "bin"), order.ExpiresAtUtc);
        }
        catch (HttpRequestException) { return PaymentErrors.Unavailable; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return PaymentErrors.Unavailable; }
        catch (JsonException) { return PaymentErrors.Unavailable; }
    }
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, "https://api-merchant.payos.vn" + path);
        request.Headers.Add("x-client-id", Setting("PayOS", "ClientId")); request.Headers.Add("x-api-key", Setting("PayOS", "ApiKey"));
        return request;
    }
    private Result<PaymentLink> CreateVnPay(PaymentOrder order, string ip)
    {
        var url = Setting("VnPay", "PaymentUrl");
        if (string.IsNullOrEmpty(url)) url = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        if (url is not ("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html" or "https://pay.vnpay.vn/vpcpay.html")) return PaymentErrors.Unavailable;
        var values = new Dictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = Setting("VnPay", "TmnCode"),
            ["vnp_Amount"] = (order.Amount * 100).ToString("0", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = order.OrderCode.ToString(CultureInfo.InvariantCulture),
            ["vnp_OrderInfo"] = "Thanh toan " + order.OrderCode,
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = order.ReturnUrl,
            ["vnp_IpAddr"] = ip,
            ["vnp_CreateDate"] = order.CreatedAtUtc.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_ExpireDate"] = order.ExpiresAtUtc.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
        };
        var query = VnPayCanonical(values);
        return new PaymentLink(order.Id, order.OrderCode, order.Gateway, order.Amount, "VND", url + "?" + query +
            "&vnp_SecureHash=" + Sign512(Setting("VnPay", "HashSecret"), query), null, null, null, null, order.ExpiresAtUtc);
    }
    public Result<VerifiedPayment> VerifyPayOs(string body)
    {
        if (!IsConfigured("PayOS")) return PaymentErrors.Unavailable;
        try
        {
            using var json = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 8 });
            if (!ValidPayOsData(json.RootElement, out var data)) return PaymentErrors.Signature;
            if (!data.TryGetProperty("orderCode", out var o) || o.ValueKind != JsonValueKind.Number || !o.TryGetInt64(out var code) || code <= 0 ||
                !data.TryGetProperty("amount", out var a) || a.ValueKind != JsonValueKind.Number || !a.TryGetDecimal(out var amount) || amount is <= 0 or > 1_000_000_000 ||
                decimal.Truncate(amount) != amount || String(data, "currency") != "VND" || String(data, "reference") is not { Length: > 0 and <= 100 } reference)
                return PaymentErrors.Payload;
            return new VerifiedPayment(code, amount, "VND", reference, String(data, "code") == "00");
        }
        catch (JsonException) { return PaymentErrors.Payload; }
    }
    public Result<VerifiedPayment> VerifyVnPay(IReadOnlyDictionary<string, string> query)
    {
        if (!IsConfigured("VnPay")) return PaymentErrors.Unavailable;
        var fields = query.Where(x => x.Key.StartsWith("vnp_", StringComparison.Ordinal) && x.Key is not ("vnp_SecureHash" or "vnp_SecureHashType"))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        if (!query.TryGetValue("vnp_SecureHash", out var signature) || !EqualsSignature(signature, Sign512(Setting("VnPay", "HashSecret"), VnPayCanonical(fields))))
            return PaymentErrors.Signature;
        if (fields.GetValueOrDefault("vnp_TmnCode") != Setting("VnPay", "TmnCode") ||
            !long.TryParse(fields.GetValueOrDefault("vnp_TxnRef"), NumberStyles.None, CultureInfo.InvariantCulture, out var order) || order <= 0 ||
            !long.TryParse(fields.GetValueOrDefault("vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount <= 0 || amount > 100_000_000_000 || amount % 100 != 0 ||
            fields.GetValueOrDefault("vnp_TransactionNo") is not { Length: > 0 and <= 100 } reference)
            return PaymentErrors.Payload;
        var success = fields.GetValueOrDefault("vnp_ResponseCode") == "00" && fields.GetValueOrDefault("vnp_TransactionStatus") == "00";
        if (success && (reference == "0" || !reference.All(char.IsAsciiDigit))) return PaymentErrors.Payload;
        return new VerifiedPayment(order, amount / 100m, "VND", reference, success);
    }
    private bool ValidPayOsData(JsonElement root, out JsonElement data)
    {
        if (!TryData(root, out data) || root.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count()) return false;
        return TryPayOsCanonical(data, out var canonical) && EqualsSignature(String(root, "signature"), Sign256(Setting("PayOS", "ChecksumKey"), canonical));
    }
    public static bool TryPayOsCanonical(JsonElement data, out string canonical)
    {
        canonical = "";
        if (HasDuplicateKeys(data)) return false;
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var valid = Flatten(data, "");
        canonical = string.Join("&", fields.Select(x => x.Key + "=" + x.Value));
        return valid;

        bool Flatten(JsonElement value, string prefix)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var properties = value.EnumerateObject().ToArray();
                if (properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) return false;
                return properties.All(x => Flatten(x.Value, prefix.Length == 0 ? x.Name : prefix + "." + x.Name));
            }
            if (value.ValueKind == JsonValueKind.Array)
            {
                var sorted = value.EnumerateArray().Select(SortValue).ToArray();
                return fields.TryAdd(prefix, JsonSerializer.Serialize(sorted, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            }
            var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ValueKind == JsonValueKind.Null ? "" : value.GetRawText();
            return fields.TryAdd(prefix, text);
        }
    }
    private static bool HasDuplicateKeys(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != value.EnumerateObject().Count() ||
            value.EnumerateObject().Any(x => HasDuplicateKeys(x.Value)),
        JsonValueKind.Array => value.EnumerateArray().Any(HasDuplicateKeys),
        _ => false
    };
    private static object? SortValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => new SortedDictionary<string, object?>(value.EnumerateObject().ToDictionary(x => x.Name, x => SortValue(x.Value)), StringComparer.Ordinal),
        JsonValueKind.Array => value.EnumerateArray().Select(SortValue).ToArray(),
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => value.Clone()
    };
    private static bool TryData(JsonElement root, out JsonElement data)
    {
        data = default;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out data) && data.ValueKind == JsonValueKind.Object;
    }
    private static string? String(JsonElement data, string key) => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    public static string VnPayCanonical(IReadOnlyDictionary<string, string> fields) => string.Join("&", fields.Where(x => !string.IsNullOrEmpty(x.Value))
        .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => WebUtility.UrlEncode(x.Key) + "=" + WebUtility.UrlEncode(x.Value)));
    public static string Sign256(string key, string content) => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(content)));
    public static string Sign512(string key, string content) => Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(content)));
    private static bool EqualsSignature(string? supplied, string expected) => supplied?.Length == expected.Length && supplied.All(char.IsAsciiHexDigit) &&
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(supplied), Convert.FromHexString(expected));
}
