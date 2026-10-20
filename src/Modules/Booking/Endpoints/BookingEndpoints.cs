using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Booking.Application;
using SkillBridge.Modules.Booking.Application.DTOs;

namespace SkillBridge.Modules.Booking.Endpoints;

public static class BookingEndpoints
{
    public static void MapBookingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/booking/bookings").WithTags("Booking").RequireAuthorization();
        group.MapPost("", async (CreateBookingRequest request, HttpContext context, ICurrentUser user, Bookings service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.CreateAsync(id, context.Request.Headers["Idempotency-Key"].ToString(), request, ct);
            return result.IsSuccess ? Results.Created($"/api/v1/booking/bookings/{result.Value.Id}", result.Value) : Failure(result.Error);
        }).RequireAuthorization(p => p.RequireRole("Mentee"));
        group.MapGet("/{bookingId:guid}", async (Guid bookingId, ICurrentUser user, Bookings service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.GetAsync(bookingId, id, user.HasRole("Admin"), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        });
        group.MapGet("", async (ICurrentUser user, Bookings service, CancellationToken ct, string? status = null, int page = 1, int pageSize = 10) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.ListAsync(id, user.HasRole("Admin"), status, page, pageSize, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        });
        group.MapPost("/{bookingId:guid}/confirm", async (Guid bookingId, ConfirmBookingRequest request, ICurrentUser user, Bookings service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.ConfirmAsync(bookingId, id, request.MentorNote, ct)))
            .RequireAuthorization(p => p.RequireRole("Mentor"));
        group.MapPost("/{bookingId:guid}/cancel", async (Guid bookingId, CancelBookingRequest request, ICurrentUser user, Bookings service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.CancelAsync(bookingId, id, request.Reason, ct)))
            .RequireAuthorization(p => p.RequireRole("Mentor", "Mentee"));
        group.MapPost("/{bookingId:guid}/complete", async (Guid bookingId, ICurrentUser user, Bookings service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.CompleteAsync(bookingId, id, ct)))
            .RequireAuthorization(p => p.RequireRole("Mentor", "Mentee"));
        group.MapGet("/{bookingId:guid}/audit-logs", async (Guid bookingId, ICurrentUser user, Bookings service, CancellationToken ct, int page = 1, int pageSize = 50) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.AuditAsync(bookingId, id, user.HasRole("Admin"), page, pageSize, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        });
        group.MapPost("/{bookingId:guid}/reschedule", async (Guid bookingId, CreateRescheduleRequest request, ICurrentUser user, Reschedules service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.CreateAsync(bookingId, id, request, ct);
            return result.IsSuccess ? Results.Created($"/api/v1/booking/bookings/{bookingId}", result.Value) : Failure(result.Error);
        }).RequireAuthorization(p => p.RequireRole("Mentor", "Mentee"));
        endpoints.MapPost("/api/v1/booking/reschedule-requests/{requestId:guid}/respond", async (Guid requestId, RespondRescheduleRequest request,
            ICurrentUser user, Reschedules service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.RespondAsync(requestId, id, request, ct)))
            .WithTags("Booking").RequireAuthorization(p => p.RequireRole("Mentor", "Mentee"));
    }

    internal static IResult Outcome(Result result) => result.IsSuccess ? Results.NoContent() : Failure(result.Error);
    internal static IResult Failure(Error error) => error.Type == ErrorType.Failure
        ? Results.Problem(statusCode: 503, title: error.Code, detail: error.Description) : error.ToProblemDetails();
}
