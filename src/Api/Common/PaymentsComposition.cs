using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Application;
using SkillBridge.Modules.Payments.Application;

namespace SkillBridge.Api.Common;

public static class PaymentsComposition
{
    public static IServiceCollection AddPaymentsComposition(this IServiceCollection services)
    {
        services.AddScoped<IPaymentBookings, PaymentBookings>();
        services.AddScoped<IBookingLifecycleConsumer, PaymentBookingLifecycle>();
        return services;
    }
}
internal sealed class PaymentBookings(BookingSnapshots snapshots, Bookings bookings) : IPaymentBookings
{
    public async Task<Result<PaymentBooking>> GetAsync(Guid id, CancellationToken ct)
    {
        var result = await snapshots.GetAsync(id, ct);
        return result.IsFailure ? result.Error : new PaymentBooking(result.Value.Id, result.Value.MenteeUserId,
            result.Value.ServiceSnapshot.MentorUserId, result.Value.TotalAmount, result.Value.Currency, result.Value.Status, result.Value.HeldUntilUtc);
    }
    public Task<Result> ApplyAsync(Guid bookingId, Guid paymentId, decimal amount, string currency, CancellationToken ct) =>
        bookings.ReceivePaymentAsync(bookingId, paymentId, amount, currency, ct);
}
internal sealed class PaymentBookingLifecycle(PaymentsService payments) : IBookingLifecycleConsumer
{
    public Task<Result> ConsumeAsync(Guid eventId, Guid bookingId, CancellationToken ct) => payments.SyncBookingAsync(bookingId, ct);
}
