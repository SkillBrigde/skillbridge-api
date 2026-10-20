using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Payments.Application;

namespace SkillBridge.Modules.Payments.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/payments").WithTags("Payments");
        group.MapGet("/wallet", async (ICurrentUser user, PaymentsService service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Results.Ok(await service.WalletAsync(id, ct))).RequireAuthorization();
        group.MapGet("/wallet/transactions", async (ICurrentUser user, PaymentsService service, CancellationToken ct, int page = 1, int pageSize = 10) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.TransactionsAsync(id, page, pageSize, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        }).RequireAuthorization();
        group.MapPost("/create-payment", async (CreatePaymentRequest request, HttpContext context, ICurrentUser user, PaymentsService service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.CreateAsync(id, context.Request.Headers["Idempotency-Key"].ToString(), request,
                context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1", ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        }).RequireAuthorization(p => p.RequireRole("Mentee"));
        group.MapGet("/bookings/{bookingId:guid}/escrow", async (Guid bookingId, ICurrentUser user, PaymentsService service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.EscrowAsync(bookingId, id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        }).RequireAuthorization();
        group.MapPost("/withdraw", (WithdrawRequest request) => Failure(PaymentsService.Withdraw(request).Error))
            .RequireAuthorization(p => p.RequireRole("Mentor"));
        group.MapGet("/payouts", (int page = 1, int pageSize = 10) =>
        {
            var offset = PaymentsService.Offset(page, pageSize);
            // Chính sách đang khóa: chưa có yêu cầu rút tiền nào được tạo hoặc thực thi.
            return offset.IsFailure ? Failure(offset.Error) : Results.Ok(PagedResult<object>.Create([], page, pageSize, 0));
        }).RequireAuthorization(p => p.RequireRole("Mentor"));
        group.MapPost("/webhooks/{gateway}", ReceiveAsync).AllowAnonymous();
        // VNPAY IPN sử dụng GET query string; đây là alias của cùng contract webhook.
        group.MapGet("/webhooks/{gateway}", ReceiveAsync).AllowAnonymous();
    }
    private static async Task<IResult> ReceiveAsync(string gateway, HttpContext context, IPaymentGateway provider, PaymentsService service, CancellationToken ct)
    {
        Result<VerifiedPayment> result;
        if (gateway == "payos" && HttpMethods.IsPost(context.Request.Method))
        {
            var bytes = new byte[65537]; var length = 0;
            while (length < bytes.Length)
            {
                var count = await context.Request.Body.ReadAsync(bytes.AsMemory(length), ct);
                if (count == 0) break;
                length += count;
            }
            if (length > 65536) return Results.StatusCode(413);
            string body;
            try { body = new UTF8Encoding(false, true).GetString(bytes, 0, length); }
            catch (DecoderFallbackException) { return Failure(PaymentErrors.Payload); }
            result = provider.VerifyPayOs(body);
        }
        else if (gateway == "vnpay")
        {
            if (context.Request.Query.Count > 40 || context.Request.QueryString.Value?.Length > 65536 || context.Request.Query.Any(x => x.Value.Count != 1))
                return Results.Json(new VnPayAcknowledgement("97", "Invalid parameters"));
            result = provider.VerifyVnPay(context.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal));
        }
        else return Results.NotFound();
        if (gateway == "payos")
        {
            if (result.IsFailure) return Failure(result.Error);
            var saved = await service.ReceiveAsync("PayOS", result.Value, ct);
            // payOS gửi giao dịch mẫu có chữ ký khi đăng ký webhook; đã lưu receipt không có đơn để đối soát.
            return saved.IsSuccess || saved.Error.Code == PaymentErrors.Missing.Code ? Results.Ok(new { success = true }) : Failure(saved.Error);
        }
        if (result.IsFailure)
            return result.Error == PaymentErrors.Unavailable ? Failure(result.Error) : Results.Json(new VnPayAcknowledgement("97", "Invalid signature or payload"));
        var received = await service.ReceiveAsync("VnPay", result.Value, ct);
        return received.IsSuccess ? Results.Json(new VnPayAcknowledgement("00", "Confirm Success")) :
            Results.Json(new VnPayAcknowledgement(received.Error.Type == ErrorType.NotFound ? "01" : "04", "Order requires reconciliation"));
    }
    public static IResult Failure(Error error) => error.Type == ErrorType.Failure ? Results.Problem(statusCode: 503, title: error.Code, detail: error.Description) : error.ToProblemDetails();
}

internal sealed record VnPayAcknowledgement([property: JsonPropertyName("RspCode")] string Code, [property: JsonPropertyName("Message")] string Message);
