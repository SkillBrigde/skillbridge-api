using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Payments.Domain;
using SkillBridge.Modules.Payments.Infrastructure;

namespace SkillBridge.Modules.Payments.Application;

public sealed class PaymentsService(PaymentsDbContext db, IPaymentBookings bookings, IPaymentGateway gateway, TimeProvider clock)
{
    public async Task<Result<PaymentLink>> CreateAsync(Guid user, string? key, CreatePaymentRequest request, string ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || key.Any(x => x is < '!' or > '~') || request.BookingId == Guid.Empty ||
            request.Gateway is not ("PayOS" or "VnPay") || request.Description is { Length: > 255 } ||
            !gateway.IsAllowedReturnUrl(request.ReturnUrl) || !gateway.IsAllowedReturnUrl(request.CancelUrl))
            return Error.Validation("Payments.InvalidRequest", "Cần booking, Idempotency-Key, gateway và URL trả về HTTPS được cấu hình cho phép.");
        if (!gateway.IsConfigured(request.Gateway)) return PaymentErrors.Unavailable;
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        Guid id;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await LockAsync("create:" + user + ":" + key, ct);
            await LockAsync("booking:" + request.BookingId, ct);
            db.ChangeTracker.Clear();
            var order = await db.Orders.SingleOrDefaultAsync(x => x.UserId == user && x.IdempotencyKey == key, ct);
            if (order is not null)
            {
                if (order.RequestHash != hash) return Error.Conflict("Payments.IdempotencyMismatch", "Key đã dùng với nội dung khác.");
                id = order.Id;
            }
            else
            {
                var booking = await bookings.GetAsync(request.BookingId, ct);
                if (booking.IsFailure) return booking.Error;
                if (booking.Value.UserId != user) return PaymentErrors.Missing;
                if (booking.Value.Status != "PendingPayment" || booking.Value.HeldUntilUtc <= clock.GetUtcNow())
                    return Error.Conflict("Payments.BookingClosed", "Booking không còn chờ thanh toán hoặc đã hết hạn.");
                if (request.Amount.HasValue && request.Amount.Value != booking.Value.Amount)
                    return Error.Conflict("Payments.AmountMismatch", "Số tiền phải khớp snapshot Booking.");
                if (await db.Orders.AnyAsync(x => x.BookingId == request.BookingId, ct))
                    return Error.Conflict("Payments.BookingPaymentExists", "Booking đã có đơn thanh toán; sử dụng lại key ban đầu.");
                var created = PaymentOrder.Create(booking.Value.Id, user, booking.Value.MentorUserId, booking.Value.Amount, booking.Value.Currency,
                    request.Gateway, key, hash, request.ReturnUrl!, request.CancelUrl!, booking.Value.HeldUntilUtc, clock.GetUtcNow());
                if (created.IsFailure) return created.Error;
                db.Orders.Add(created.Value); await db.SaveChangesAsync(ct); id = created.Value.Id;
            }
            await tx.CommitAsync(ct);
        }
        await using var linkTx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(id.ToString(), ct); db.ChangeTracker.Clear();
        var payment = await db.Orders.SingleAsync(x => x.Id == id, ct);
        if (payment.LinkJson is not null) return JsonSerializer.Deserialize<PaymentLink>(payment.LinkJson)!;
        if (payment.ExpiresAtUtc <= clock.GetUtcNow() || payment.Status != "Creating")
            return Error.Conflict("Payments.PaymentClosed", "Đơn thanh toán đã đóng hoặc hết hạn.");
        var link = await gateway.CreateAsync(payment, ip, ct);
        if (link.IsFailure) return link.Error;
        payment.AttachLink(JsonSerializer.Serialize(link.Value)); await db.SaveChangesAsync(ct); await linkTx.CommitAsync(ct);
        return link.Value;
    }

    public async Task<Result> ReceiveAsync(string name, VerifiedPayment verified, CancellationToken ct)
    {
        if (!verified.Success) return Result.Success();
        Guid? paymentId = null;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await LockAsync("receipt:" + name + ":" + verified.Reference, ct);
            db.ChangeTracker.Clear();
            var previous = await db.Receipts.SingleOrDefaultAsync(x => x.Gateway == name && x.Reference == verified.Reference, ct);
            if (previous is not null)
            {
                if (previous.OrderCode != verified.OrderCode || previous.Amount != verified.Amount || previous.Currency != verified.Currency)
                    return Result.Failure(Error.Conflict("Payments.ReferenceReused", "Mã giao dịch đã có nội dung khác."));
                return previous.RejectionCode is null ? Result.Success() : Result.Failure(Error.Conflict(previous.RejectionCode, "Giao dịch đã được lưu để đối soát thủ công."));
            }
            var id = await db.Orders.Where(x => x.OrderCode == verified.OrderCode && x.Gateway == name).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            Error rejection = PaymentErrors.Missing;
            if (id.HasValue)
            {
                await LockAsync(id.Value.ToString(), ct);
                var order = await db.Orders.SingleAsync(x => x.Id == id.Value, ct);
                var accepted = order.Verify(verified.Reference, verified.Amount, verified.Currency, clock.GetUtcNow());
                rejection = accepted.Error;
                if (accepted.IsSuccess)
                {
                    paymentId = order.Id;
                    db.Escrows.Add(PaymentEscrow.From(order, clock.GetUtcNow()));
                    db.Ledger.AddRange(LedgerEntry.Receive(order, clock.GetUtcNow()));
                }
            }
            db.Receipts.Add(new GatewayReceipt
            {
                Gateway = name,
                Reference = verified.Reference,
                OrderCode = verified.OrderCode,
                Amount = verified.Amount,
                Currency = verified.Currency,
                RejectionCode = rejection == Error.None ? null : rejection.Code,
                ReceivedAtUtc = clock.GetUtcNow()
            });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            if (rejection != Error.None) return Result.Failure(rejection);
        }
        // Tiền và ledger đã bền vững trước khi gọi Booking; worker tiếp tục nếu tiến trình bị ngắt.
        if (paymentId.HasValue) await ApplyAsync(paymentId.Value, ct);
        return Result.Success();
    }
    public async Task<Result> ApplyAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(id.ToString(), ct); db.ChangeTracker.Clear();
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null || order.Status != "Verified") return Result.Success();
        var applied = await bookings.ApplyAsync(order.BookingId, order.Id, order.Amount, order.Currency, ct);
        if (applied.IsFailure && applied.Error.Type is not (ErrorType.Conflict or ErrorType.NotFound)) return applied;
        order.FinishBooking(applied.IsSuccess);
        if (applied.IsFailure) (await db.Escrows.SingleAsync(x => x.PaymentId == id, ct)).RequireReview();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Result.Success();
    }
    public async Task<Result> SyncBookingAsync(Guid bookingId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync("escrow:" + bookingId, ct); db.ChangeTracker.Clear();
        var booking = await bookings.GetAsync(bookingId, ct);
        if (booking.IsFailure) return Result.Failure(booking.Error);
        if (booking.Value.Status is "Cancelled" or "RefundReview")
        {
            var escrow = await db.Escrows.SingleOrDefaultAsync(x => x.BookingId == bookingId, ct);
            escrow?.RequireReview();
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return Result.Success();
    }
    public async Task<object> WalletAsync(Guid user, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync("wallet:" + user, ct); db.ChangeTracker.Clear();
        var wallet = await db.Wallets.SingleOrDefaultAsync(x => x.Id == user, ct);
        if (wallet is null) { wallet = Wallet.Create(user, clock.GetUtcNow()); db.Wallets.Add(wallet); await db.SaveChangesAsync(ct); }
        var held = await db.Escrows.Where(x => x.UserId == user).SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0;
        await tx.CommitAsync(ct);
        return new
        {
            id = wallet.Id,
            userId = user,
            balance = 0m,
            availableBalance = 0m,
            heldBalance = held,
            currency = "VND",
            createdAtUtc = wallet.CreatedAtUtc,
            updatedAtUtc = wallet.CreatedAtUtc,
            disbursementLocked = true
        };
    }
    public async Task<Result<PagedResult<WalletTransaction>>> TransactionsAsync(Guid user, int page, int size, CancellationToken ct)
    {
        var offset = Offset(page, size); if (offset.IsFailure) return offset.Error;
        var query = db.Escrows.AsNoTracking().Where(x => x.UserId == user);
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(offset.Value).Take(size)
            .Select(x => new WalletTransaction(x.Id, user, "GatewayEscrowHold", x.TotalAmount, 0, 0, x.BookingId, "Booking", x.CreatedAtUtc)).ToListAsync(ct);
        return PagedResult<WalletTransaction>.Create(items, page, size, count);
    }
    public async Task<Result<object>> EscrowAsync(Guid bookingId, Guid user, CancellationToken ct)
    {
        var row = await db.Escrows.AsNoTracking().SingleOrDefaultAsync(x => x.BookingId == bookingId && (x.UserId == user || x.MentorUserId == user), ct);
        return row is null ? PaymentErrors.Missing : new
        {
            row.Id,
            row.BookingId,
            row.TotalAmount,
            row.Currency,
            row.Status,
            platformFeeAmount = (decimal?)null,
            mentorNetAmount = (decimal?)null,
            releasedAtUtc = (DateTimeOffset?)null,
            row.CreatedAtUtc,
            disbursementLocked = true
        };
    }
    public static Result Withdraw(WithdrawRequest request) => request.Amount is <= 0 or > 1_000_000_000 ||
        string.IsNullOrWhiteSpace(request.BankCode) || request.BankCode.Length > 20 || !request.BankCode.All(char.IsAsciiLetterOrDigit) ||
        request.AccountNumber is not { Length: >= 6 and <= 34 } || !request.AccountNumber.All(char.IsAsciiDigit) ||
        string.IsNullOrWhiteSpace(request.AccountName) || request.AccountName.Length > 100 || request.AccountName.Any(char.IsControl)
        ? Result.Failure(Error.Validation("Payments.InvalidWithdrawal", "Số tiền và thông tin ngân hàng không hợp lệ.")) : Result.Failure(DisbursementPolicy.Locked);
    public static Result<int> Offset(int page, int size) => page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue
        ? Error.Validation("Payments.InvalidPage", "Phân trang không hợp lệ.") : (page - 1) * size;
    private Task LockAsync(string key, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"payments:" + key}, 0))", ct);
}
public sealed record WalletTransaction(Guid Id, Guid WalletId, string Type, decimal Amount, decimal BalanceBefore, decimal BalanceAfter,
    Guid ReferenceId, string ReferenceType, DateTimeOffset CreatedAtUtc);
