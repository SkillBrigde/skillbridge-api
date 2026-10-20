using MediatR;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Application;
using SkillBridge.Modules.Booking.Domain;
using SkillBridge.Modules.Catalog.Application;
using SkillBridge.Modules.Identity.Application.Users;
using SkillBridge.Modules.Profiles.Application.Mentors;
using SkillBridge.Modules.Scheduling.Application;

namespace SkillBridge.Api.Common;

public static class BookingComposition
{
    public static IServiceCollection AddBookingComposition(this IServiceCollection services)
    {
        services.AddScoped<IBookingServiceDirectory, CatalogBookingServices>();
        services.AddScoped<IBookingSlotReservations, SchedulingBookingReservations>();
        return services;
    }
}

internal sealed class CatalogBookingServices(MentorServices services, ISender sender) : IBookingServiceDirectory
{
    public async Task<Result<ServiceTerms>> GetAsync(Guid serviceId, CancellationToken ct)
    {
        var result = await services.GetBookingSnapshotAsync(serviceId, ct);
        if (result.IsFailure) return result.Error;
        var service = result.Value;
        var owner = await sender.Send(new GetUserQuery(service.MentorUserId), ct);
        if (owner.IsFailure || !owner.Value.IsActive || owner.Value.Role != "Mentor")
            return Error.Conflict("Booking.MentorUnavailable", "Tài khoản Mentor hiện không nhận đặt lịch.");
        var profile = await sender.Send(new GetMentorProfileQuery(service.MentorUserId), ct);
        if (profile.IsFailure) return profile.Error;
        if (profile.Value.Id != service.MentorId || !profile.Value.IsAcceptingMentees || profile.Value.VerificationStatus == "Rejected")
            return Error.Conflict("Booking.MentorUnavailable", "Mentor hiện không nhận đặt lịch.");
        return new ServiceTerms(service.ServiceId, service.ServiceVersion, service.MentorId, service.MentorUserId,
            service.ServiceTitle, service.ServiceDescription, service.ServiceType, service.Deliverables, service.Price,
            service.Currency, service.DurationMinutes, service.TotalSessions, service.CapturedAtUtc);
    }
}

internal sealed class SchedulingBookingReservations(SlotReservations reservations) : IBookingSlotReservations
{
    public async Task<Result<HeldPackage>> HoldAsync(Guid mentorId, Guid bookingId, IReadOnlyList<Guid> slots, int duration,
        DateTimeOffset until, CancellationToken ct)
    {
        var result = await reservations.HoldAsync(mentorId, bookingId, slots, duration, until, ct);
        return result.IsFailure ? result.Error : new HeldPackage(result.Value.HeldUntilUtc,
            result.Value.Slots.Select(x => new SessionWindow(x.SlotId, x.StartAtUtc, x.EndAtUtc)).ToList());
    }
    public Task<Result> ConfirmAsync(Guid mentorId, Guid bookingId, int count, CancellationToken ct) => reservations.ConfirmAsync(mentorId, bookingId, count, ct);
    public Task<Result> ReleaseAsync(Guid mentorId, Guid bookingId, CancellationToken ct) => reservations.ReleaseAsync(mentorId, bookingId, ct);
    public async Task<Result<SessionWindow>> MoveAsync(Guid mentorId, Guid bookingId, Guid oldSlot, Guid newSlot, int duration, CancellationToken ct)
    {
        var result = await reservations.MoveBookedAsync(mentorId, bookingId, oldSlot, newSlot, duration, ct);
        return result.IsFailure ? result.Error : new SessionWindow(result.Value.SlotId, result.Value.StartAtUtc, result.Value.EndAtUtc);
    }
}
