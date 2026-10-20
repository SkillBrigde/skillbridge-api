using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Payments.Domain;

namespace SkillBridge.Modules.Payments.Application;

public sealed record PaymentBooking(Guid Id, Guid UserId, Guid MentorUserId, decimal Amount, string Currency, string Status, DateTimeOffset HeldUntilUtc);
public interface IPaymentBookings
{
    Task<Result<PaymentBooking>> GetAsync(Guid id, CancellationToken ct);
    Task<Result> ApplyAsync(Guid bookingId, Guid paymentId, decimal amount, string currency, CancellationToken ct);
}
internal sealed class UnavailablePaymentBookings : IPaymentBookings
{
    public Task<Result<PaymentBooking>> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<Result<PaymentBooking>>(PaymentErrors.Unavailable);
    public Task<Result> ApplyAsync(Guid bookingId, Guid paymentId, decimal amount, string currency, CancellationToken ct) => Task.FromResult(Result.Failure(PaymentErrors.Unavailable));
}
public sealed record CreatePaymentRequest(Guid BookingId, decimal? Amount, string? Gateway, string? Description, string? ReturnUrl, string? CancelUrl);
public sealed record WithdrawRequest(decimal Amount, string? BankCode, string? AccountNumber, string? AccountName);
public sealed record PaymentLink(Guid PaymentId, long OrderCode, string Gateway, decimal Amount, string Currency,
    string PaymentUrl, string? QrCode, string? AccountNumber, string? AccountName, string? Bin, DateTimeOffset ExpiresAtUtc);
public sealed record VerifiedPayment(long OrderCode, decimal Amount, string Currency, string Reference, bool Success);
public interface IPaymentGateway
{
    bool IsConfigured(string gateway);
    bool IsAllowedReturnUrl(string? url);
    Task<Result<PaymentLink>> CreateAsync(PaymentOrder order, string clientIp, CancellationToken ct);
    Result<VerifiedPayment> VerifyPayOs(string body);
    Result<VerifiedPayment> VerifyVnPay(IReadOnlyDictionary<string, string> query);
}
public static class PaymentErrors
{
    public static readonly Error Unavailable = Error.Failure("Payments.Unavailable", "Thiếu cấu hình hoặc cổng thanh toán tạm thời không khả dụng.");
    public static readonly Error Missing = Error.NotFound("Payments.NotFound", "Không tìm thấy thanh toán.");
    public static readonly Error Signature = Error.Unauthorized("Payments.InvalidSignature", "Chữ ký thanh toán không hợp lệ.");
    public static readonly Error Payload = Error.Validation("Payments.InvalidPayload", "Dữ liệu cổng thanh toán không hợp lệ.");
}
