using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Application.DTOs;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity.Application.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IQuery<UserDto>;

public sealed class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, UserDto>
{
    private readonly IdentityDbContext _dbContext;

    public GetCurrentUserQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return Result<UserDto>.Failure(Error.NotFound(
                "Identity.UserNotFound",
                "Không tìm thấy thông tin tài khoản người dùng."));
        }

        var dto = new UserDto(
            Id: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role,
            PhoneNumber: user.PhoneNumber,
            AvatarUrl: user.AvatarUrl,
            IsEmailConfirmed: user.IsEmailConfirmed,
            IsActive: user.IsActive,
            CreatedAtUtc: user.CreatedAtUtc
        );

        return Result<UserDto>.Success(dto);
    }
}
