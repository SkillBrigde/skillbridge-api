using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Application.Users.DTOs.Responses;
using SkillBridge.Modules.Identity.Infrastructure.Persistence;

namespace SkillBridge.Modules.Identity.Application.Users;

public sealed record GetCurrentUserQuery(Guid UserId) : IQuery<CurrentUserResponse>;

public sealed class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    private readonly IdentityDbContext _dbContext;

    public GetCurrentUserQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return Result<CurrentUserResponse>.Failure(Error.NotFound(
                "Identity.UserNotFound",
                "Không tìm thấy thông tin tài khoản người dùng."));
        }

        var dto = new CurrentUserResponse(
            Id: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role,
            IsActive: user.IsActive
        );

        return Result<CurrentUserResponse>.Success(dto);
    }
}
