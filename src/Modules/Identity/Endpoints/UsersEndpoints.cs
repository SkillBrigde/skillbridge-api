using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results.Pagination;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Application.Users.GetCurrentUser;
using SkillBridge.Modules.Identity.Application.Users.GetUsers;
using SkillBridge.Modules.Identity.Application.Users.Requests;
using SkillBridge.Modules.Identity.Application.Users.Responses;
using SkillBridge.Modules.Identity.Application.Users.UpdateUserStatus;

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
        .Produces<UserResponse>()
        .WithName("GetCurrentUser")
        .WithSummary("Lấy thông tin tài khoản người dùng đang đăng nhập");

        group.MapGet("/", async (
            [AsParameters] GetUsersRequest request,
            ISender sender) =>
        {
            var query = new GetUsersQuery(
                PageNumber: request.Page ?? 1,
                PageSize: request.PageSize ?? 20,
                SearchTerm: request.Search,
                Role: request.Role,
                IsActive: request.IsActive
            );

            var result = await sender.Send(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .Produces<PagedResult<UserResponse>>()
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
