using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Application.Commands.ChangePassword;
using SkillBridge.Modules.Identity.Application.Commands.Login;
using SkillBridge.Modules.Identity.Application.Commands.Logout;
using SkillBridge.Modules.Identity.Application.Commands.RefreshToken;
using SkillBridge.Modules.Identity.Application.Commands.Register;

namespace SkillBridge.Modules.Identity.Endpoints;

public static class AuthEndpoints
{
    private const string CookieName = "refreshToken";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity/auth").WithTags("Identity Auth").RequireRateLimiting("auth");

        group.MapPost("/register", async (RegisterUserCommand command, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(command, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/identity/users/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemDetails();
        }).AllowAnonymous().WithName("Register");

        group.MapPost("/login", async (LoginCommand command, HttpContext context, ISender sender, JwtSettings settings, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(command with { IpAddress = context.Connection.RemoteIpAddress?.ToString() }, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails();
            }

            context.Response.Cookies.Append(CookieName, result.Value.RefreshToken, CookieOptions(settings.RefreshTokenExpirationDays));
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { result.Value.AccessToken, result.Value.ExpiresIn });
        }).AllowAnonymous().WithName("Login");

        group.MapPost("/refresh", async (HttpContext context, ISender sender, JwtSettings settings, CancellationToken cancellationToken) =>
        {
            var token = context.Request.Cookies[CookieName];
            var result = await sender.Send(new RefreshTokenCommand(token ?? "", context.Connection.RemoteIpAddress?.ToString()), cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails();
            }

            context.Response.Cookies.Append(CookieName, result.Value.RefreshToken, CookieOptions(settings.RefreshTokenExpirationDays));
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { result.Value.AccessToken, result.Value.ExpiresIn });
        }).AllowAnonymous().WithName("RefreshToken");

        group.MapPost("/logout", async (HttpContext context, ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new LogoutCommand(currentUser.UserId.Value, context.Request.Cookies[CookieName]), cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails();
            }

            context.Response.Cookies.Delete(CookieName, CookieOptions(0));
            return Results.NoContent();
        }).RequireAuthorization().WithName("Logout");

        group.MapPost("/change-password", async (ChangePasswordRequest request, HttpContext context, ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new ChangePasswordCommand(currentUser.UserId.Value, request.CurrentPassword, request.NewPassword), cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails();
            }

            context.Response.Cookies.Delete(CookieName, CookieOptions(0));
            return Results.NoContent();
        }).RequireAuthorization().WithName("ChangePassword");
    }

    private static CookieOptions CookieOptions(int expirationDays) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/identity/auth",
        MaxAge = TimeSpan.FromDays(expirationDays)
    };
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
