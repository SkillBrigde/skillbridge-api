using SkillBridge.Modules.Scheduling.Domain;

namespace SkillBridge.Modules.Scheduling.Application.DTOs.Responses;

public sealed record SlotResponse(Guid Id, Guid MentorId, DateTimeOffset StartAtUtc, DateTimeOffset EndAtUtc,
    string Status, Guid? BookingId, DateTimeOffset CreatedAtUtc)
{
    public static SlotResponse From(AvailabilitySlot slot) => new(slot.Id, slot.MentorId, slot.StartAtUtc,
        slot.EndAtUtc, slot.Status, null, slot.CreatedAtUtc);
}

public sealed record TimeOffResponse(Guid Id, Guid MentorId, DateOnly StartDate, DateOnly EndDate, string? Reason, DateTimeOffset CreatedAtUtc)
{
    public static TimeOffResponse From(TimeOff item) => new(item.Id, item.MentorId, item.StartDate, item.EndDate, item.Reason, item.CreatedAtUtc);
}
