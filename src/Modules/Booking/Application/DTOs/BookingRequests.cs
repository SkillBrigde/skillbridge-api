using System.Text.Json.Serialization;
using SkillBridge.Modules.Booking.Domain;

namespace SkillBridge.Modules.Booking.Application.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateBookingRequest(Guid ServiceId, long ServiceVersion, IReadOnlyList<Guid>? SlotIds, BookingIntakeRequest? IntakeForm);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BookingIntakeRequest(string? CoreQuestion, string? AttachedLinks, string? TargetGoals)
{
    public IntakeForm ToDomain() => new(CoreQuestion?.Trim() ?? "", AttachedLinks?.Trim() ?? "", TargetGoals?.Trim() ?? "");
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmBookingRequest(string? MentorNote);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelBookingRequest(string? Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateRescheduleRequest(Guid BookingSessionId, Guid NewSlotId, string? Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RespondRescheduleRequest(bool? IsApproved, string? ResponseNote);
