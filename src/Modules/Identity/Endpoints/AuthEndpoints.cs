using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Application.Auth;
using SkillBridge.Modules.Identity.Application.Auth.DTOs.Requests;
using SkillBridge.Modules.Identity.Application.Auth.DTOs.Responses;
using SkillBridge.Modules.Identity.Endpoints.RateLimiting;

namespace SkillBridge.Modules.Identity.Endpoints;

public static class AuthEndpoints
{
    private const string CookieName = "refreshToken";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity/auth").WithTags("Identity Auth").RequireRateLimiting("auth");

        group.MapPost("/register", async (RegisterUserRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new RegisterUserCommand(request.Email, request.Password, request.FullName, request.Role, request.PhoneNumber);
            var result = await sender.Send(command, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/identity/users/{result.Value}", new RegisterUserResponse(result.Value))
                : result.Error.ToProblemDetails();
        }).AllowAnonymous().WithName("Register");

        group.MapPost("/login", async (LoginRequest request, HttpContext context, ISender sender, JwtSettings settings, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new LoginCommand(request.Email, request.Password, context.Connection.RemoteIpAddress?.ToString()), cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails();
            }

            if (!string.IsNullOrEmpty(result.Value.RefreshToken))
                context.Response.Cookies.Append(CookieName, result.Value.RefreshToken, CookieOptions(settings.RefreshTokenExpirationDays));
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new TokenResponse(result.Value.AccessToken, result.Value.ExpiresIn));
        }).AddEndpointFilter<LoginRateLimitFilter>().AllowAnonymous().WithName("Login");

        group.MapPost("/refresh", async (HttpContext context, ISender sender, JwtSettings settings, CancellationToken cancellationToken) =>
        {
            if (!IsTrustedBrowserRefresh(context.Request))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var token = context.Request.Cookies[CookieName];
            var result = await sender.Send(new RefreshTokenCommand(token ?? "", context.Connection.RemoteIpAddress?.ToString()), cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails();
            }

            if (!string.IsNullOrEmpty(result.Value.RefreshToken))
                context.Response.Cookies.Append(CookieName, result.Value.RefreshToken, CookieOptions(settings.RefreshTokenExpirationDays));
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new TokenResponse(result.Value.AccessToken, result.Value.ExpiresIn));
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

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = await sender.Send(new ForgotPasswordCommand(request.Email), cancellationToken);
            if (result.IsSuccess) return Results.NoContent();
            return result.Error.Code == "Identity.PasswordRecoveryUnavailable"
                ? Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: result.Error.Code, detail: result.Error.Description)
                : result.Error.ToProblemDetails();
        }).AllowAnonymous().WithName("ForgotPassword");

        group.MapPost("/reset-password", async (ResetPasswordRequest request, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = await sender.Send(new ResetPasswordCommand(request.Email, request.Token, request.NewPassword), cancellationToken);
            if (result.IsFailure) return result.Error.ToProblemDetails();
            context.Response.Cookies.Delete(CookieName, CookieOptions(0));
            return Results.NoContent();
        }).AllowAnonymous().WithName("ResetPassword");
    }

    private static CookieOptions CookieOptions(int expirationDays) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/identity/auth",
        MaxAge = TimeSpan.FromDays(expirationDays)
    };

    private static bool IsTrustedBrowserRefresh(HttpRequest request)
    {
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length > 0 && fetchSite != "same-origin") return false;

        var sourceHeader = request.Headers.Origin.ToString();
        if (sourceHeader.Length == 0)
            sourceHeader = request.Headers.Referer.ToString();
        if (sourceHeader.Length == 0) return true; // Server-to-server refresh from the web BFF.

        return Uri.TryCreate(sourceHeader, UriKind.Absolute, out var source)
            && source.Scheme is "http" or "https"
            && string.Equals(source.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase)
            && string.Equals(source.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase);
    }
}
