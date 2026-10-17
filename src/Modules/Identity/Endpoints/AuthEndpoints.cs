using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Application.Auth.ChangePassword;
using SkillBridge.Modules.Identity.Application.Auth.Login;
using SkillBridge.Modules.Identity.Application.Auth.Logout;
using SkillBridge.Modules.Identity.Application.Auth.RefreshToken;
using SkillBridge.Modules.Identity.Application.Auth.Register;
using SkillBridge.Modules.Identity.Application.Auth.Requests;
using SkillBridge.Modules.Identity.Application.Auth.Responses;
using SkillBridge.Modules.Identity.Endpoints.RateLimiting;
namespace SkillBridge.Modules.Identity.Endpoints;

public static class AuthEndpoints
{
    private const string RefreshTokenCookieName = "__Host-refresh";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity/auth")
            .WithTags("Identity Auth");

        group.MapPost("/register", async (RegisterUserRequest request, ISender sender) =>
        {
            var command = new RegisterUserCommand(
                request.Email,
                request.Password,
                request.FullName,
                request.Role,
                request.PhoneNumber);
            var result = await sender.Send(command);
            return result.ToHttpResult();
        })
        .Produces<AuthResponse>()
        .WithName("Register")
        .WithSummary("Đăng ký tài khoản mới (Mentor hoặc Mentee)");

        group.MapPost("/login", async (LoginRequest request, HttpContext httpContext, ISender sender) =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var command = new LoginCommand(request.Email, request.Password, ip);
            var result = await sender.Send(command);

            if (result.IsSuccess && !string.IsNullOrEmpty(result.Value.RefreshToken))
            {
                AppendRefreshTokenCookie(httpContext, result.Value.RefreshToken);
            }

            return result.ToHttpResult();
        })
        .AddEndpointFilter<LoginRateLimitFilter>()
        .Produces<AuthResponse>()
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
        .WithName("Login")
        .WithSummary("Đăng nhập email/mật khẩu, cấp Access Token và Refresh Token");

        group.MapPost("/refresh", async (RefreshTokenRequest? request, HttpContext httpContext, ISender sender) =>
        {
            var token = GetRefreshToken(httpContext, request?.RefreshToken);
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var result = await sender.Send(new RefreshTokenCommand(token ?? "", ip));

            if (result.IsSuccess && !string.IsNullOrEmpty(result.Value.RefreshToken))
            {
                AppendRefreshTokenCookie(httpContext, result.Value.RefreshToken);
            }

            return result.ToHttpResult();
        })
        .Produces<AuthResponse>()
        .WithName("RefreshToken")
        .WithSummary("Cấp mới Access Token bằng Refresh Token (Token Rotation)");

        group.MapPost("/logout", async (LogoutRequest? request, HttpContext httpContext, ISender sender) =>
        {
            var token = GetRefreshToken(httpContext, request?.RefreshToken);
            var result = await sender.Send(new LogoutCommand(token));
            httpContext.Response.Cookies.Delete(RefreshTokenCookieName);

            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("Logout")
        .WithSummary("Đăng xuất và thu hồi Refresh Token");

        group.MapPost("/change-password", async (ChangePasswordRequest request, ICurrentUser currentUser, ISender sender) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var command = new ChangePasswordCommand(currentUser.UserId.Value, request.CurrentPassword, request.NewPassword);
            var result = await sender.Send(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("ChangePassword")
        .WithSummary("Đổi mật khẩu tài khoản");
    }

    private static string? GetRefreshToken(HttpContext httpContext, string? bodyToken)
    {
        if (string.IsNullOrEmpty(bodyToken))
        {
            httpContext.Request.Cookies.TryGetValue(RefreshTokenCookieName, out bodyToken);
        }

        return bodyToken;
    }

    private static void AppendRefreshTokenCookie(HttpContext httpContext, string refreshToken)
    {
        httpContext.Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}
