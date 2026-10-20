using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Reviews.Application;

namespace SkillBridge.Modules.Reviews.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/reviews").WithTags("Reviews");
        group.MapPost("", async (CreateReviewRequest request, ICurrentUser user, ReviewsService service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.CreateAsync(id, request, ct);
            return result.IsSuccess ? Results.Created($"/api/v1/reviews/{result.Value.Id}", result.Value) : Failure(result.Error);
        }).RequireAuthorization(p => p.RequireRole("Mentee"));
        group.MapGet("/{reviewId:guid}", async (Guid reviewId, ReviewsService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(reviewId, ct); return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        }).AllowAnonymous();
        group.MapPut("/{reviewId:guid}", async (Guid reviewId, UpdateReviewRequest request, ICurrentUser user, ReviewsService service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.UpdateAsync(reviewId, id, request, ct))).RequireAuthorization(p => p.RequireRole("Mentee"));
        group.MapPost("/{reviewId:guid}/reply", async (Guid reviewId, ReplyReviewRequest request, ICurrentUser user, ReviewsService service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.ReplyAsync(reviewId, id, request.Reply, ct))).RequireAuthorization(p => p.RequireRole("Mentor"));
        group.MapGet("/mentors/{mentorId:guid}", async (Guid mentorId, ReviewsService service, CancellationToken ct, int page = 1, int pageSize = 10) =>
        {
            var result = await service.ListAsync(mentorId, page, pageSize, ct); return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        }).AllowAnonymous();
        group.MapGet("/mentors/{mentorId:guid}/summary", async (Guid mentorId, ReviewsService service, CancellationToken ct) => Results.Ok(await service.SummaryAsync(mentorId, ct))).AllowAnonymous();
        group.MapGet("/my-pending", async (ICurrentUser user, ReviewsService service, CancellationToken ct, int page = 1, int pageSize = 10) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.PendingAsync(id, page, pageSize, ct);
            if (result.IsFailure) return Failure(result.Error);
            return Results.Ok(new
            {
                items = result.Value.Items.Select(x => new { x.BookingId, x.MentorId, sessionId = x.CompletedSessionIds[0] }),
                page,
                result.Value.PageSize,
                result.Value.TotalCount,
                result.Value.TotalPages
            });
        }).RequireAuthorization(p => p.RequireRole("Mentee"));
    }
    private static IResult Outcome(Result result) => result.IsSuccess ? Results.NoContent() : Failure(result.Error);
    private static IResult Failure(Error error) => error.Type == ErrorType.Failure ? Results.Problem(statusCode: 503, title: error.Code, detail: error.Description) : error.ToProblemDetails();
}
