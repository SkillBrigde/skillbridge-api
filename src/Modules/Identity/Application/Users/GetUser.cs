using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Application.Users.DTOs.Responses;
using SkillBridge.Modules.Identity.Infrastructure.Persistence;

namespace SkillBridge.Modules.Identity.Application.Users;

public sealed record GetUserQuery(Guid UserId) : IQuery<UserDetailsResponse>;

public sealed class GetUserQueryHandler(IdentityDbContext dbContext) : IQueryHandler<GetUserQuery, UserDetailsResponse>
{
    public async Task<Result<UserDetailsResponse>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == request.UserId)
            .Select(user => new UserDetailsResponse(user.Id, user.Email, user.FullName, user.Role, user.IsActive,
                user.TwoFactorEnabled, user.LockoutEndUtc, user.AccessFailedCount, user.CreatedAtUtc, user.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
        return user is null
            ? Result<UserDetailsResponse>.Failure(Error.NotFound("Identity.UserNotFound", "Không tìm thấy người dùng."))
            : Result<UserDetailsResponse>.Success(user);
    }
}
