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
    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity/auth")
            .WithTags("Identity Auth");

        group.MapPost("/register", async (RegisterUserCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.ToHttpResult();
        })
        .WithName("Register")
        .WithSummary("Đăng ký tài khoản mới (Mentor hoặc Mentee)");

        group.MapPost("/login", async (LoginCommand command, HttpContext httpContext, ISender sender) =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var result = await sender.Send(command with { IpAddress = ip });

            if (result.IsSuccess && !string.IsNullOrEmpty(result.Value.RefreshToken))
            {
                // Đính kèm Refresh Token vào HttpOnly Cookie bảo mật cao
                httpContext.Response.Cookies.Append("__Host-refresh", result.Value.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });
            }

            return result.ToHttpResult();
        })
        .WithName("Login")
        .WithSummary("Đăng nhập email/mật khẩu, cấp Access Token và Refresh Token");

        group.MapPost("/refresh", async (RefreshTokenRequest? request, HttpContext httpContext, ISender sender) =>
        {
            // Đọc Refresh Token từ Cookie hoặc từ Request Body
            var token = request?.RefreshToken;
            if (string.IsNullOrEmpty(token))
            {
                httpContext.Request.Cookies.TryGetValue("__Host-refresh", out token);
            }

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var result = await sender.Send(new RefreshTokenCommand(token ?? "", ip));

            if (result.IsSuccess && !string.IsNullOrEmpty(result.Value.RefreshToken))
            {
                httpContext.Response.Cookies.Append("__Host-refresh", result.Value.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });
            }

            return result.ToHttpResult();
        })
        .WithName("RefreshToken")
        .WithSummary("Cấp mới Access Token bằng Refresh Token (Token Rotation)");

        group.MapPost("/logout", async (LogoutRequest? request, HttpContext httpContext, ISender sender) =>
        {
            var token = request?.RefreshToken;
            if (string.IsNullOrEmpty(token))
            {
                httpContext.Request.Cookies.TryGetValue("__Host-refresh", out token);
            }

            var result = await sender.Send(new LogoutCommand(token));
            httpContext.Response.Cookies.Delete("__Host-refresh");

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
}

public sealed record RefreshTokenRequest(string? RefreshToken);
public sealed record LogoutRequest(string? RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
