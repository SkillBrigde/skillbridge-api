using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Scheduling.Application;
using SkillBridge.Modules.Scheduling.Application.DTOs.Requests;

namespace SkillBridge.Modules.Scheduling.Endpoints;

public static class SlotEndpoints
{
    public static void MapSlotEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/scheduling").WithTags("Scheduling");
        group.MapGet("/mentors/{mentorId:guid}/slots", async (Guid mentorId, DateOnly startDate, DateOnly endDate,
            SlotService service, CancellationToken ct, int page = 1, int pageSize = 100) =>
            (await service.ListSlotsAsync(mentorId, startDate, endDate, page, pageSize, ct)).ToHttpResult()).AllowAnonymous();
        var owner = group.MapGroup("/time-offs").RequireAuthorization(p => p.RequireRole("Mentor"));
        owner.MapPost("", async (TimeOffRequest request, ICurrentUser user, SlotService service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.AddTimeOffAsync(id, request, ct);
            return result.IsSuccess ? Results.Created($"/api/v1/scheduling/time-offs/{result.Value.Id}", result.Value) : result.Error.ToProblemDetails();
        });
        owner.MapDelete("/{timeOffId:guid}", async (Guid timeOffId, ICurrentUser user, SlotService service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.RemoveTimeOffAsync(id, timeOffId, ct);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        });
        owner.MapGet("", async (ICurrentUser user, SlotService service, CancellationToken ct, int page = 1, int pageSize = 50) =>
            user.UserId is not { } id ? Results.Unauthorized() : (await service.ListTimeOffsAsync(id, page, pageSize, ct)).ToHttpResult());
    }
}
