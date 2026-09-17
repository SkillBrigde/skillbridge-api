using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Application;

namespace SkillBridge.Modules.Identity.Endpoints;

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var identity = endpoints.MapGroup("/api/v1/identity").WithTags("Identity");
        var auth = identity.MapGroup("/auth").RequireRateLimiting("strict-auth");
        auth.MapPost("/register", async (RegisterRequest request, IdentityService service, CancellationToken ct) =>
        {
            var result = await service.RegisterAsync(request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/identity/users/{result.Value.Id}", result.Value)
                : result.Error.ToProblemDetails();
        }).AllowAnonymous();
        auth.MapPost("/login", async (LoginRequest request, HttpContext context, IdentityService service, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return (await service.LoginAsync(request, context.Request.Headers.UserAgent.ToString(),
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", ct)).ToHttpResult();
        }).AllowAnonymous();
        auth.MapPost("/refresh", async (RefreshRequest request, HttpContext context, IdentityService service, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return (await service.RefreshAsync(request, ct)).ToHttpResult();
        }).AllowAnonymous();
        auth.MapPost("/logout", async (ClaimsPrincipal user, IdentityService service, CancellationToken ct) =>
        {
            var result = await service.RevokeSessionsAsync(UserId(user), SessionId(user), SessionId(user), ct);
            return result.IsSuccess ? Results.Ok(new { isSuccess = true }) : result.Error.ToProblemDetails();
        }).RequireAuthorization();
        auth.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal user,
            IdentityService service, CancellationToken ct) =>
        {
            var result = await service.ChangePasswordAsync(UserId(user), request, ct);
            return result.IsSuccess ? Results.Ok(new { isSuccess = true, message = "Vui lòng đăng nhập lại." }) :
                result.Error.ToProblemDetails();
        }).RequireAuthorization();

        var users = identity.MapGroup("/users").RequireAuthorization();
        users.MapGet("/me", async (ClaimsPrincipal user, IdentityService service, CancellationToken ct) =>
            (await service.GetUserAsync(UserId(user), ct)).ToHttpResult());
        users.MapPut("/me", async (UpdateProfileRequest request, ClaimsPrincipal user, IdentityService service, CancellationToken ct) =>
            (await service.UpdateProfileAsync(UserId(user), request, ct)).ToHttpResult());
        users.MapGet("/", async (IdentityService service, CancellationToken ct, string? search = null,
            string? role = null, string? status = null, int page = 1, int pageSize = 20) =>
            (await service.GetUsersAsync(search, role, status, page, pageSize, ct)).ToHttpResult()).RequireAuthorization("Admin");
        users.MapGet("/{userId:guid}", async (Guid userId, IdentityService service, CancellationToken ct) =>
            (await service.GetUserAsync(userId, ct)).ToHttpResult()).RequireAuthorization("Admin");
        users.MapPut("/{userId:guid}/status", async (Guid userId, SetStatusRequest request, ClaimsPrincipal user,
            IdentityService service, CancellationToken ct) =>
            (await service.SetStatusAsync(UserId(user), userId, request, ct)).ToHttpResult()).RequireAuthorization("Admin");

        var sessions = endpoints.MapGroup("/api/v1/sessions").WithTags("Sessions").RequireAuthorization();
        sessions.MapGet("/", async (ClaimsPrincipal user, IdentityService service, CancellationToken ct) =>
        {
            var result = await service.GetSessionsAsync(UserId(user), SessionId(user), ct);
            return result.IsSuccess ? Results.Ok(new { items = result.Value }) : result.Error.ToProblemDetails();
        });
        sessions.MapDelete("/other", async (ClaimsPrincipal user, IdentityService service, CancellationToken ct) =>
        {
            var result = await service.RevokeSessionsAsync(UserId(user), SessionId(user), null, ct);
            return result.IsSuccess ? Results.Ok(new { revokedSessionsCount = result.Value }) : result.Error.ToProblemDetails();
        });
        sessions.MapDelete("/{sessionId:guid}", async (Guid sessionId, ClaimsPrincipal user, IdentityService service, CancellationToken ct) =>
        {
            var result = await service.RevokeSessionsAsync(UserId(user), SessionId(user), sessionId, ct);
            return result.IsSuccess ? Results.Ok(new { isSuccess = true }) : result.Error.ToProblemDetails();
        });
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static Guid SessionId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sid")!);
}
