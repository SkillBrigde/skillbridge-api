using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Domain;

namespace SkillBridge.Modules.Booking.Application;

public interface IBookingServiceDirectory
{
    Task<Result<ServiceTerms>> GetAsync(Guid serviceId, CancellationToken ct);
}

public sealed record HeldPackage(DateTimeOffset Until, IReadOnlyList<SessionWindow> Windows);

public interface IBookingSlotReservations
{
    Task<Result<HeldPackage>> HoldAsync(Guid mentorId, Guid bookingId, IReadOnlyList<Guid> slots, int duration,
        DateTimeOffset until, CancellationToken ct);
    Task<Result> ConfirmAsync(Guid mentorId, Guid bookingId, int count, CancellationToken ct);
    Task<Result> ReleaseAsync(Guid mentorId, Guid bookingId, CancellationToken ct);
    Task<Result<SessionWindow>> MoveAsync(Guid mentorId, Guid bookingId, Guid oldSlot, Guid newSlot, int duration, CancellationToken ct);
}

public interface IBookingCompletionEvidence
{
    Task<Result<IReadOnlyCollection<Guid>>> GetCompletedSessionsAsync(Guid bookingId, CancellationToken ct);
}

public static class BookingErrors
{
    public static readonly Error Missing = Error.NotFound("Booking.NotFound", "Không tìm thấy booking.");
    public static readonly Error Dependency = Error.Failure("Booking.DependencyUnavailable", "Module phụ thuộc chưa khả dụng.");
    public static readonly Error Idempotency = Error.Conflict("Booking.IdempotencyMismatch", "Idempotency-Key đã dùng với nội dung khác.");
}

internal sealed class UnavailableBookingDependencies : IBookingServiceDirectory, IBookingSlotReservations, IBookingCompletionEvidence
{
    public Task<Result<ServiceTerms>> GetAsync(Guid serviceId, CancellationToken ct) => Task.FromResult<Result<ServiceTerms>>(BookingErrors.Dependency);
    public Task<Result<HeldPackage>> HoldAsync(Guid mentorId, Guid bookingId, IReadOnlyList<Guid> slots, int duration,
        DateTimeOffset until, CancellationToken ct) => Task.FromResult<Result<HeldPackage>>(BookingErrors.Dependency);
    public Task<Result> ConfirmAsync(Guid mentorId, Guid bookingId, int count, CancellationToken ct) => Task.FromResult(Result.Failure(BookingErrors.Dependency));
    public Task<Result> ReleaseAsync(Guid mentorId, Guid bookingId, CancellationToken ct) => Task.FromResult(Result.Failure(BookingErrors.Dependency));
    public Task<Result<SessionWindow>> MoveAsync(Guid mentorId, Guid bookingId, Guid oldSlot, Guid newSlot, int duration, CancellationToken ct) =>
        Task.FromResult<Result<SessionWindow>>(BookingErrors.Dependency);
    public Task<Result<IReadOnlyCollection<Guid>>> GetCompletedSessionsAsync(Guid bookingId, CancellationToken ct) =>
        Task.FromResult<Result<IReadOnlyCollection<Guid>>>(BookingErrors.Dependency);
}
