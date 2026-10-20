using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Recommendations.Application;

namespace SkillBridge.Modules.Recommendations.Endpoints;

public static class RecommendationEndpoints
{
    public static void MapRecommendationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/recommendations").WithTags("Recommendations");
        group.MapGet("/mentors", async (ICurrentUser user, RecommendationsService service, CancellationToken ct, int page = 1, int pageSize = 20) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.MentorsAsync(id, null, page, pageSize, ct); return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        }).RequireAuthorization(p => p.RequireRole("Mentee"));
        group.MapGet("/mentors/for-skill/{skillId:guid}", async (Guid skillId, RecommendationsService service, CancellationToken ct, int page = 1, int pageSize = 10) =>
        {
            var result = await service.MentorsAsync(Guid.Empty, skillId, page, pageSize, ct); return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        }).AllowAnonymous();
        group.MapPost("/interactions", async (InteractionRequest request, ICurrentUser user, RecommendationsService service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.InteractAsync(id, request, ct); return result.IsSuccess ? Results.NoContent() : Failure(result.Error);
        }).RequireAuthorization(p => p.RequireRole("Mentee"));
        group.MapGet("/mentors/{mentorId:guid}/metrics", async (Guid mentorId, ICurrentUser user, RecommendationsService service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.MetricsAsync(mentorId, id, user.HasRole("Admin"), ct); return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        }).RequireAuthorization(p => p.RequireRole("Mentor", "Admin"));
        group.MapGet("/trending-skills", async (RecommendationsService service, CancellationToken ct) =>
        {
            var result = await service.TrendingAsync(ct); return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
        }).AllowAnonymous();
    }
    private static IResult Failure(Error error) => error.Type == ErrorType.Failure ? Results.Problem(statusCode: 503, title: error.Code, detail: error.Description) : error.ToProblemDetails();
}
