using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Application.Commands.UpdateCurrentUser;
using SkillBridge.Modules.Identity.Application.Commands.UpdateUserStatus;
using SkillBridge.Modules.Identity.Application.Queries.GetCurrentUser;
using SkillBridge.Modules.Identity.Application.Queries.GetUser;
using SkillBridge.Modules.Identity.Application.Queries.GetUsers;

namespace SkillBridge.Modules.Identity.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity/users").WithTags("Identity Users").RequireAuthorization();

        group.MapGet("/me", async (ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetCurrentUserQuery(currentUser.UserId.Value), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(new { result.Value.Id, result.Value.Email, result.Value.FullName, result.Value.Role, result.Value.IsActive })
                : result.Error.ToProblemDetails();
        }).WithName("GetCurrentUser");

        group.MapPut("/me", async (UpdateCurrentUserRequest request, ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new UpdateCurrentUserCommand(currentUser.UserId.Value, request.FullName), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).WithName("UpdateCurrentUser");

        group.MapGet("/", async (int? page, int? pageSize, string? searchTerm, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetUsersQuery(page ?? 1, pageSize ?? 10, searchTerm), cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemDetails();
            }

            var users = result.Value;
            return Results.Ok(new
            {
                items = users.Items.Select(user => new { user.Id, user.Email, user.FullName, user.Role, user.IsActive, user.CreatedAtUtc }),
                page = users.PageNumber,
                users.PageSize,
                users.TotalCount,
                users.TotalPages
            });
        }).RequireAuthorization(policy => policy.RequireRole("Admin")).WithName("GetUsers");

        group.MapGet("/{userId:guid}", async (Guid userId, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetUserQuery(userId), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(policy => policy.RequireRole("Admin")).WithName("GetUser");

        group.MapPut("/{userId:guid}/status", async (Guid userId, UpdateUserStatusRequest request, ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!request.IsActive.HasValue)
            {
                return Error.Validation("Identity.IsActiveRequired", "Trạng thái isActive là bắt buộc.").ToProblemDetails();
            }

            if (userId == currentUser.UserId && !request.IsActive.Value)
            {
                return Error.Forbidden("Identity.SelfDeactivation", "Không thể tự khóa tài khoản.").ToProblemDetails();
            }

            var result = await sender.Send(new UpdateUserStatusCommand(userId, request.IsActive.Value), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).RequireAuthorization(policy => policy.RequireRole("Admin")).WithName("UpdateUserStatus");
    }
}

public sealed record UpdateCurrentUserRequest(string FullName);
public sealed record UpdateUserStatusRequest(bool? IsActive);
