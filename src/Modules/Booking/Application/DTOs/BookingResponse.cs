using SkillBridge.Modules.Booking.Domain;

namespace SkillBridge.Modules.Booking.Application.DTOs;

public sealed record BookingSessionResponse(Guid Id, int SessionNumber, Guid SlotId, DateTimeOffset? StartAtUtc, DateTimeOffset? EndAtUtc);
public sealed record BookingResponse(Guid Id, string BookingCode, Guid MenteeUserId, Guid MentorId, string Status,
    ServiceTerms ServiceSnapshot, IntakeForm IntakeForm, decimal TotalAmount, string Currency,
    DateTimeOffset HeldUntilUtc, string? MentorNote, string? CancellationReason, Guid? CancelledBy,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, IReadOnlyList<BookingSessionResponse> Sessions,
    IReadOnlyList<RescheduleResponse> RescheduleRequests)
{
    internal static BookingResponse From(BookingOrder order) => new(order.Id, order.BookingCode, order.MenteeUserId,
        order.Terms.MentorId, order.Status, order.Terms, order.Intake, order.Terms.Price, order.Terms.Currency,
        order.HeldUntilUtc, order.MentorNote, order.CancellationReason, order.CancelledBy, order.CreatedAtUtc, order.UpdatedAtUtc,
        order.Sessions.OrderBy(x => x.SessionNumber).Select(x => new BookingSessionResponse(x.Id, x.SessionNumber, x.SlotId, x.StartAtUtc, x.EndAtUtc)).ToList(),
        order.RescheduleRequests.OrderBy(x => x.CreatedAtUtc).Select(RescheduleResponse.From).ToList());
}

public sealed record RescheduleResponse(Guid Id, Guid BookingSessionId, Guid OldSlotId, Guid NewSlotId, Guid RequestedByUserId,
    string Reason, string Status, string? ResponseNote, DateTimeOffset CreatedAtUtc)
{
    internal static RescheduleResponse From(RescheduleRequest request) => new(request.Id, request.BookingSessionId, request.OldSlotId,
        request.NewSlotId, request.RequestedByUserId, request.Reason, request.Status, request.ResponseNote, request.CreatedAtUtc);
}

public sealed record BookingAuditResponse(Guid Id, string? OldStatus, string NewStatus, Guid? ChangedByUserId, string Reason, DateTimeOffset CreatedAtUtc);
