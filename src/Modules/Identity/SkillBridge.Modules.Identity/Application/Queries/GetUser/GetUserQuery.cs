using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity.Application.Queries.GetUser;

public sealed record UserDetailsDto(Guid Id, string Email, string FullName, string Role, bool IsActive,
    bool TwoFactorEnabled, DateTimeOffset? LockoutEndUtc, int AccessFailedCount,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);

public sealed record GetUserQuery(Guid UserId) : IQuery<UserDetailsDto>;

public sealed class GetUserQueryHandler(IdentityDbContext dbContext) : IQueryHandler<GetUserQuery, UserDetailsDto>
{
    public async Task<Result<UserDetailsDto>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == request.UserId)
            .Select(user => new UserDetailsDto(user.Id, user.Email, user.FullName, user.Role, user.IsActive,
                user.TwoFactorEnabled, user.LockoutEndUtc, user.AccessFailedCount, user.CreatedAtUtc, user.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
        return user is null
            ? Result<UserDetailsDto>.Failure(Error.NotFound("Identity.UserNotFound", "Không tìm thấy người dùng."))
            : Result<UserDetailsDto>.Success(user);
    }
}
