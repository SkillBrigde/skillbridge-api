using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Application.DTOs;
using SkillBridge.Modules.Booking.Domain;
using SkillBridge.Modules.Booking.Infrastructure;

namespace SkillBridge.Modules.Booking.Application;

public sealed class Bookings(BookingDbContext db, IBookingServiceDirectory services, IBookingSlotReservations reservations,
    IBookingCompletionEvidence completion, TimeProvider clock)
{
    public async Task<Result<BookingResponse>> CreateAsync(Guid user, string? key, CreateBookingRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || key.Any(x => x is < '!' or > '~') ||
            request.ServiceId == Guid.Empty || request.ServiceVersion < 1 || request.SlotIds is not { Count: >= 1 and <= 52 } ||
            request.SlotIds.Any(x => x == Guid.Empty) || request.SlotIds.Distinct().Count() != request.SlotIds.Count || request.IntakeForm is null)
            return Error.Validation("Booking.InvalidRequest", "Cần Idempotency-Key (1–100 ký tự ASCII), serviceId, serviceVersion, slotIds và intakeForm hợp lệ.");
        var intake = request.IntakeForm.ToDomain();
        var valid = intake.Validate();
        if (valid.IsFailure) return valid.Error;
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { request.ServiceId, request.ServiceVersion, Slots = request.SlotIds.Order().ToArray(), Intake = intake })));
        Guid id;
        // Ghi ý định bền vững trước khi gọi Scheduling để có thể khôi phục sau khi tiến trình dừng.
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await LockAsync($"create:{user}:{key}", ct);
            var existing = await db.Bookings.SingleOrDefaultAsync(x => x.MenteeUserId == user && x.IdempotencyKey == key, ct);
            if (existing is not null)
            {
                if (existing.RequestHash != hash) return BookingErrors.Idempotency;
                id = existing.Id;
            }
            else
            {
                var terms = await services.GetAsync(request.ServiceId, ct);
                if (terms.IsFailure) return terms.Error;
                if (terms.Value.Version != request.ServiceVersion)
                    return Error.Conflict("Booking.ServiceChanged", "Dịch vụ đã thay đổi; hãy tải lại giá và số buổi.");
                var created = BookingOrder.Create(user, terms.Value, intake, request.SlotIds, key, hash, clock.GetUtcNow());
                if (created.IsFailure) return created.Error;
                db.Bookings.Add(created.Value);
                await db.SaveChangesAsync(ct);
                id = created.Value.Id;
            }
            await transaction.CommitAsync(ct);
        }
        db.ChangeTracker.Clear();
        var held = await ResumeHoldAsync(id, ct);
        if (held.IsFailure) return held.Error;
        return await GetAsync(id, user, false, ct);
    }

    public async Task<Result<BookingResponse>> GetAsync(Guid id, Guid user, bool admin, CancellationToken ct)
    {
        var order = await db.Bookings.AsNoTracking().Include(x => x.Sessions).Include(x => x.RescheduleRequests).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id && (admin || x.MenteeUserId == user || x.MentorUserId == user), ct);
        return order is null ? BookingErrors.Missing : BookingResponse.From(order);
    }

    public async Task<Result<PagedResult<BookingResponse>>> ListAsync(Guid user, bool admin, string? status, int page, int pageSize, CancellationToken ct)
    {
        var offset = Offset(page, pageSize);
        if (offset.IsFailure) return offset.Error;
        if (status is not null && status is not ("Holding" or "PendingPayment" or "AwaitingMentor" or "Confirmed" or "Completed" or "Cancelled" or "RefundReview"))
            return Error.Validation("Booking.InvalidStatus", "Trạng thái booking không hợp lệ.");
        var query = db.Bookings.AsNoTracking().Where(x => admin || x.MenteeUserId == user || x.MentorUserId == user);
        if (status is not null) query = query.Where(x => x.Status == status);
        var count = await query.LongCountAsync(ct);
        var items = await query.Include(x => x.Sessions).Include(x => x.RescheduleRequests).AsSplitQuery().OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip(offset.Value).Take(pageSize).ToListAsync(ct);
        return PagedResult<BookingResponse>.Create(items.Select(BookingResponse.From).ToList(), page, pageSize, count);
    }

    public async Task<Result> ConfirmAsync(Guid id, Guid user, string? note, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(id.ToString(), ct);
        var order = await LoadAsync(id, ct);
        if (order is null || order.MentorUserId != user) return Result.Failure(BookingErrors.Missing);
        var old = order.Status;
        var confirmed = order.Confirm(user, note, clock.GetUtcNow());
        if (confirmed.IsFailure) return confirmed;
        await SaveTransitionAsync(order, old, ct);
        await tx.CommitAsync(ct);
        return Result.Success();
    }

    public async Task<Result> CancelAsync(Guid id, Guid user, string? reason, CancellationToken ct)
    {
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await LockAsync(id.ToString(), ct);
            var order = await LoadAsync(id, ct);
            if (order is null) return Result.Failure(BookingErrors.Missing);
            var old = order.Status;
            var cancelled = order.Cancel(user, reason ?? "", clock.GetUtcNow());
            if (cancelled.IsFailure) return cancelled;
            await SaveTransitionAsync(order, old, ct);
            await tx.CommitAsync(ct);
        }
        db.ChangeTracker.Clear();
        // Hủy đã bền vững; worker tiếp tục nhả slot nếu Scheduling tạm thời lỗi.
        await ReconcileAsync(id, ct);
        return Result.Success();
    }

    public async Task<Result> CompleteAsync(Guid id, Guid user, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(id.ToString(), ct);
        var order = await LoadAsync(id, ct);
        if (order is null || user != order.MenteeUserId && user != order.MentorUserId) return Result.Failure(BookingErrors.Missing);
        if (order.Status == "Completed") return Result.Success();
        if (order.Status != "Confirmed") return Result.Failure(Error.Conflict("Booking.SessionsIncomplete", "Booking chưa được xác nhận."));
        var evidence = await completion.GetCompletedSessionsAsync(id, ct);
        if (evidence.IsFailure) return Result.Failure(evidence.Error);
        var old = order.Status;
        var result = order.Complete(user, evidence.Value, clock.GetUtcNow());
        if (result.IsFailure) return result;
        await SaveTransitionAsync(order, old, ct);
        await tx.CommitAsync(ct);
        return Result.Success();
    }

    public async Task<Result<PagedResult<BookingAuditResponse>>> AuditAsync(Guid id, Guid user, bool admin, int page, int pageSize, CancellationToken ct)
    {
        var offset = Offset(page, pageSize);
        if (offset.IsFailure) return offset.Error;
        if (!await db.Bookings.AnyAsync(x => x.Id == id && (admin || x.MenteeUserId == user || x.MentorUserId == user), ct))
            return BookingErrors.Missing;
        var query = db.AuditLogs.AsNoTracking().Where(x => x.BookingId == id);
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(offset.Value).Take(pageSize)
            .Select(x => new BookingAuditResponse(x.Id, x.OldStatus, x.NewStatus, x.ChangedByUserId, x.Reason, x.CreatedAtUtc)).ToListAsync(ct);
        return PagedResult<BookingAuditResponse>.Create(items, page, pageSize, count);
    }

    // Chỉ adapter Payments tin cậy được gọi; không có REST endpoint nhận trạng thái "đã trả tiền" từ client.
    public async Task<Result> ReceivePaymentAsync(Guid id, Guid paymentId, decimal amount, string currency, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync($"payment:{paymentId}", ct);
        await LockAsync(id.ToString(), ct);
        var order = await LoadAsync(id, ct);
        if (order is null) return Result.Failure(BookingErrors.Missing);
        if (paymentId != Guid.Empty && order.PaymentId == paymentId)
            return amount == order.Terms.Price && currency == order.Terms.Currency ? Result.Success() :
                Result.Failure(Error.Conflict("Booking.PaymentMismatch", "Nội dung thanh toán thử lại không khớp snapshot."));
        if (await db.Bookings.AnyAsync(x => x.PaymentId == paymentId && x.Id != id, ct))
            return Result.Failure(Error.Conflict("Booking.PaymentReused", "Thanh toán đã thuộc booking khác."));
        var old = order.Status;
        var received = order.ReceivePayment(paymentId, amount, currency, clock.GetUtcNow());
        if (received.IsFailure) return received;
        var confirmed = await reservations.ConfirmAsync(order.Terms.MentorId, id, order.Terms.TotalSessions, ct);
        if (confirmed.IsFailure) return confirmed;
        await SaveTransitionAsync(order, old, ct);
        await tx.CommitAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ResumeHoldAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(id.ToString(), ct);
        var order = await LoadAsync(id, ct);
        if (order is null) return Result.Failure(BookingErrors.Missing);
        if (order.Status != "Holding") return order.Status == "Cancelled" && order.Sessions.All(x => !x.StartAtUtc.HasValue)
            ? Result.Failure(Error.Conflict("Booking.ReservationFailed", "Booking không giữ được đủ slot; hãy chọn lịch khác và dùng khóa mới.")) : Result.Success();
        var now = clock.GetUtcNow();
        order.Expire(now);
        if (order.Status != "Holding")
        {
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Result.Failure(Error.Conflict("Booking.HoldExpired", "Booking đã hết hạn giữ chỗ."));
        }
        var held = await reservations.HoldAsync(order.Terms.MentorId, id, order.Sessions.Select(x => x.SlotId).ToList(),
            order.Terms.DurationMinutes, order.HeldUntilUtc, ct);
        if (held.IsFailure)
        {
            if (held.Error.Type == ErrorType.Failure) return Result.Failure(held.Error);
            order.RejectHold(held.Error.Description, now);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Result.Failure(held.Error);
        }
        var finished = order.FinishHold(held.Value.Windows, held.Value.Until, clock.GetUtcNow());
        if (finished.IsFailure) order.RejectHold(finished.Error.Description, clock.GetUtcNow());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return finished;
    }

    public async Task ReconcileAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(id.ToString(), ct);
        var order = await LoadAsync(id, ct);
        if (order is null) return;
        var old = order.Status;
        order.Expire(clock.GetUtcNow());
        if (order.NeedsSlotRelease)
        {
            var released = await reservations.ReleaseAsync(order.Terms.MentorId, id, ct);
            if (released.IsSuccess) order.SlotsReleased();
        }
        await SaveTransitionAsync(order, old, ct);
        await tx.CommitAsync(ct);
    }

    internal Task<BookingOrder?> LoadAsync(Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        return db.Bookings.Include(x => x.Sessions).Include(x => x.AuditLogs).Include(x => x.RescheduleRequests).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    internal Task LockAsync(string key, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"booking:" + key}, 0))", ct);

    private async Task SaveTransitionAsync(BookingOrder order, string old, CancellationToken ct)
    {
        if (old != order.Status && order.Status is "Confirmed" or "Cancelled" or "RefundReview" or "Completed")
            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = $"Booking.{order.Status}.v1",
                OccurredOnUtc = clock.GetUtcNow(),
                Content = JsonSerializer.Serialize(BookingResponse.From(order))
            });
        await db.SaveChangesAsync(ct);
    }

    internal static Result<int> Offset(int page, int size) => page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue
        ? Error.Validation("Booking.InvalidPagination", "page tối thiểu 1, pageSize 1–100, offset không vượt Int32.MaxValue.") : (page - 1) * size;
}
