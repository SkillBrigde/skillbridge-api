using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Learning.Application;

namespace SkillBridge.Modules.Learning.Endpoints;

public static class LearningEndpoints
{
    public static void MapLearningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/learning/sessions").WithTags("Learning").RequireAuthorization();
        group.MapGet("", async (ICurrentUser user, LearningSessions service, CancellationToken ct, Guid? bookingId = null,
            string? status = null, int page = 1, int pageSize = 10) => user.UserId is not { } id ? Results.Unauthorized() :
                Read(await service.ListAsync(id, user.HasRole("Admin"), bookingId, status, page, pageSize, ct)));
        group.MapGet("/{sessionId:guid}", async (Guid sessionId, ICurrentUser user, LearningSessions service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Read(await service.GetAsync(sessionId, id, user.HasRole("Admin"), ct)));
        group.MapPost("/{sessionId:guid}/start", async (Guid sessionId, StartSessionRequest request, ICurrentUser user, LearningSessions service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.StartAsync(sessionId, id, request.RoomUrl, ct)))
            .RequireAuthorization(p => p.RequireRole("Mentor"));
        group.MapPost("/{sessionId:guid}/end", async (Guid sessionId, ICurrentUser user, LearningSessions service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.EndAsync(sessionId, id, ct)))
            .RequireAuthorization(p => p.RequireRole("Mentor"));
        group.MapPost("/{sessionId:guid}/join", async (Guid sessionId, ICurrentUser user, LearningSessions service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.JoinAsync(sessionId, id, ct)))
            .RequireAuthorization(p => p.RequireRole("Mentor", "Mentee"));
        group.MapPost("/{sessionId:guid}/leave", async (Guid sessionId, ICurrentUser user, LearningSessions service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.LeaveAsync(sessionId, id, ct)))
            .RequireAuthorization(p => p.RequireRole("Mentor", "Mentee"));
        group.MapGet("/{sessionId:guid}/notes", async (Guid sessionId, ICurrentUser user, LearningSessions service, CancellationToken ct, int page = 1, int pageSize = 50) =>
            user.UserId is not { } id ? Results.Unauthorized() : Read(await service.NotesAsync(sessionId, id, user.HasRole("Admin"), page, pageSize, ct)));
        group.MapPost("/{sessionId:guid}/notes", async (Guid sessionId, NoteRequest request, ICurrentUser user, LearningSessions service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Created(await service.AddNoteAsync(sessionId, id, request, ct), $"/api/v1/learning/sessions/{sessionId}/notes"))
            .RequireAuthorization(p => p.RequireRole("Mentor", "Mentee"));
        group.MapPut("/{sessionId:guid}/notes/{noteId:guid}", async (Guid sessionId, Guid noteId, NoteRequest request, ICurrentUser user, LearningSessions service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.UpdateNoteAsync(sessionId, noteId, id, request, ct)))
            .RequireAuthorization(p => p.RequireRole("Mentor", "Mentee"));
        group.MapDelete("/{sessionId:guid}/notes/{noteId:guid}", async (Guid sessionId, Guid noteId, ICurrentUser user, LearningSessions service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.DeleteNoteAsync(sessionId, noteId, id, ct)))
            .RequireAuthorization(p => p.RequireRole("Mentor", "Mentee"));
        group.MapGet("/{sessionId:guid}/materials", async (Guid sessionId, ICurrentUser user, LearningSessions service, CancellationToken ct, int page = 1, int pageSize = 50) =>
            user.UserId is not { } id ? Results.Unauthorized() : Read(await service.MaterialsAsync(sessionId, id, user.HasRole("Admin"), page, pageSize, ct)));
        group.MapPost("/{sessionId:guid}/materials", async (Guid sessionId, HttpRequest request, ICurrentUser user, LearningSessions service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            if (request.ContentLength > LearningSessions.MaxMaterialBytes) return Results.StatusCode(413);
            return Created(await service.UploadAsync(sessionId, id, request.Headers["X-File-Name"].ToString(), request.ContentType, request.Body, ct),
                $"/api/v1/learning/sessions/{sessionId}/materials");
        }).RequireAuthorization(p => p.RequireRole("Mentor", "Mentee"));
        group.MapDelete("/{sessionId:guid}/materials/{materialId:guid}", async (Guid sessionId, Guid materialId, ICurrentUser user, LearningSessions service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.DeleteMaterialAsync(sessionId, materialId, id, ct)))
            .RequireAuthorization(p => p.RequireRole("Mentor", "Mentee"));
        group.MapGet("/{sessionId:guid}/materials/{materialId:guid}/download", async (Guid sessionId, Guid materialId, HttpResponse response,
            ICurrentUser user, LearningSessions service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.DownloadAsync(sessionId, materialId, id, user.HasRole("Admin"), ct);
            if (result.IsFailure) return Failure(result.Error);
            response.Headers.CacheControl = "private, no-store"; response.Headers.XContentTypeOptions = "nosniff";
            response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            return Results.File(result.Value.Data, result.Value.ContentType, result.Value.FileName);
        });
        group.MapGet("/{sessionId:guid}/action-items", async (Guid sessionId, ICurrentUser user, LearningSessions service, CancellationToken ct, int page = 1, int pageSize = 50) =>
            user.UserId is not { } id ? Results.Unauthorized() : Read(await service.ActionsAsync(sessionId, id, user.HasRole("Admin"), page, pageSize, ct)));
        group.MapPost("/{sessionId:guid}/action-items", async (Guid sessionId, ActionItemRequest request, ICurrentUser user, LearningSessions service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Created(await service.AddActionAsync(sessionId, id, request, ct), $"/api/v1/learning/sessions/{sessionId}/action-items"))
            .RequireAuthorization(p => p.RequireRole("Mentor"));
        group.MapPatch("/{sessionId:guid}/action-items/{itemId:guid}", async (Guid sessionId, Guid itemId, ActionCompletionRequest request,
            ICurrentUser user, LearningSessions service, CancellationToken ct) =>
            user.UserId is not { } id ? Results.Unauthorized() : Outcome(await service.CompleteActionAsync(sessionId, itemId, id, request.IsCompleted, ct)))
            .RequireAuthorization(p => p.RequireRole("Mentee"));
    }
    private static IResult Read<T>(Result<T> result) => result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error);
    private static IResult Created<T>(Result<T> result, string location) => result.IsSuccess ? Results.Created(location, result.Value) : Failure(result.Error);
    private static IResult Outcome(Result result) => result.IsSuccess ? Results.NoContent() : Failure(result.Error);
    private static IResult Failure(Error error) => error.Code == "Learning.MaterialTooLarge" ? Results.Problem(statusCode: 413, title: error.Code, detail: error.Description) :
        error.Type == ErrorType.Failure ? Results.Problem(statusCode: 503, title: error.Code, detail: error.Description) : error.ToProblemDetails();
}
