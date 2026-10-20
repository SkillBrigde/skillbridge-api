using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Payments.Domain;

public sealed class PaymentOrder : AggregateRoot<Guid>
{
    private PaymentOrder() { }
    public long OrderCode { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid MentorUserId { get; private set; }
    public string Gateway { get; private set; } = "";
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public string IdempotencyKey { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string ReturnUrl { get; private set; } = "";
    public string CancelUrl { get; private set; } = "";
    public string Status { get; private set; } = "Creating";
    public string? GatewayReference { get; private set; }
    public string? LinkJson { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public static Result<PaymentOrder> Create(Guid booking, Guid user, Guid mentor, decimal amount, string currency,
        string gateway, string key, string hash, string returnUrl, string cancelUrl, DateTimeOffset expires, DateTimeOffset now)
    {
        if (booking == Guid.Empty || user == Guid.Empty || mentor == Guid.Empty || amount is <= 0 or > 1_000_000_000 ||
            decimal.Truncate(amount) != amount || currency != "VND" || gateway is not ("PayOS" or "VnPay") || expires <= now)
            return Error.Validation("Payments.InvalidOrder", "Cổng thanh toán chỉ nhận booking VND nguyên đồng, còn thời hạn giữ chỗ.");
        return new PaymentOrder
        {
            Id = Guid.CreateVersion7(),
            BookingId = booking,
            UserId = user,
            MentorUserId = mentor,
            Amount = amount,
            Gateway = gateway,
            IdempotencyKey = key,
            RequestHash = hash,
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl,
            CreatedAtUtc = now,
            ExpiresAtUtc = expires
        };
    }
    public void AttachLink(string json) { LinkJson = json; if (Status == "Creating") Status = "AwaitingPayment"; }
    public Result Verify(string reference, decimal amount, string currency, DateTimeOffset now)
    {
        if (amount != Amount || currency != Currency || string.IsNullOrWhiteSpace(reference) || reference.Length > 100)
            return Result.Failure(Error.Conflict("Payments.PaymentMismatch", "Giao dịch không khớp số tiền, loại tiền hoặc mã tham chiếu."));
        if (GatewayReference is not null)
            return GatewayReference == reference ? Result.Success() : Result.Failure(Error.Conflict("Payments.ReferenceChanged", "Đơn đã có giao dịch khác; cần đối soát."));
        GatewayReference = reference; VerifiedAtUtc = now; Status = "Verified";
        return Result.Success();
    }
    public void FinishBooking(bool applied) => Status = applied ? "BookingApplied" : "ReviewRequired";
}

public sealed class PaymentEscrow : AggregateRoot<Guid>
{
    private PaymentEscrow() { }
    public Guid PaymentId { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid MentorUserId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public string Status { get; private set; } = "Holding";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public static PaymentEscrow From(PaymentOrder order, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        PaymentId = order.Id,
        BookingId = order.BookingId,
        UserId = order.UserId,
        MentorUserId = order.MentorUserId,
        TotalAmount = order.Amount,
        Currency = order.Currency,
        CreatedAtUtc = now
    };
    public void RequireReview() => Status = "ReviewRequired";
}

public sealed class LedgerEntry : AggregateRoot<Guid>
{
    private LedgerEntry() { }
    public Guid PaymentId { get; private set; }
    public string Account { get; private set; } = "";
    public decimal Amount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public static IReadOnlyList<LedgerEntry> Receive(PaymentOrder order, DateTimeOffset now) =>
    [new() { Id = Guid.CreateVersion7(), PaymentId = order.Id, Account = "GatewayClearing", Amount = order.Amount, CreatedAtUtc = now },
     new() { Id = Guid.CreateVersion7(), PaymentId = order.Id, Account = "EscrowLiability", Amount = -order.Amount, CreatedAtUtc = now }];
}

public sealed class Wallet : AggregateRoot<Guid>
{
    private Wallet() { }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public static Wallet Create(Guid user, DateTimeOffset now) => new() { Id = user, CreatedAtUtc = now };
}

public static class DisbursementPolicy
{
    public static readonly Error Locked = Error.Conflict("Payments.DisbursementLocked",
        "Rút tiền, giải ngân và hoàn tiền tự động bị khóa cho tới khi chốt phí, hoàn tiền và thời hạn khiếu nại.");
}

public sealed class GatewayReceipt : AggregateRoot<Guid>
{
    public string Gateway { get; init; } = "";
    public string Reference { get; init; } = "";
    public long OrderCode { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public string? RejectionCode { get; init; }
    public DateTimeOffset ReceivedAtUtc { get; init; }
    public GatewayReceipt() { Id = Guid.CreateVersion7(); }
}
