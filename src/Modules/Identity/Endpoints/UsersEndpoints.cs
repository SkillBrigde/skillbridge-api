using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Application.Commands.UpdateUserStatus;
using SkillBridge.Modules.Identity.Application.Queries.GetCurrentUser;
using SkillBridge.Modules.Identity.Application.Queries.GetUsers;

namespace SkillBridge.Modules.Identity.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity/users")
            .WithTags("Identity Users");

        group.MapGet("/me", async (ICurrentUser currentUser, ISender sender) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetCurrentUserQuery(currentUser.UserId.Value));
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser")
        .WithSummary("Lấy thông tin tài khoản người dùng đang đăng nhập");

        group.MapGet("/", async (
            int? page,
            int? pageSize,
            string? search,
            string? role,
            bool? isActive,
            ISender sender) =>
        {
            var query = new GetUsersQuery(
                PageNumber: page ?? 1,
                PageSize: pageSize ?? 20,
                SearchTerm: search,
                Role: role,
                IsActive: isActive
            );

            var result = await sender.Send(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("GetUsers")
        .WithSummary("Lấy danh sách người dùng toàn hệ thống (Dành cho Quản trị viên)");

        group.MapPut("/{userId:guid}/status", async (
            Guid userId,
            UpdateUserStatusRequest request,
            ISender sender) =>
        {
            var command = new UpdateUserStatusCommand(userId, request.IsActive);
            var result = await sender.Send(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("UpdateUserStatus")
        .WithSummary("Khóa hoặc mở khóa tài khoản người dùng (Dành cho Quản trị viên)");
    }
}

public sealed record UpdateUserStatusRequest(bool IsActive);
