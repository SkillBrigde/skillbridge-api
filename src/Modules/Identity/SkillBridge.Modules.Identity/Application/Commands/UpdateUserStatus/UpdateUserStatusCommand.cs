using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity.Application.Commands.UpdateUserStatus;

public sealed record UpdateUserStatusCommand(Guid UserId, bool IsActive) : ICommand;

public sealed class UpdateUserStatusCommandHandler : ICommandHandler<UpdateUserStatusCommand>
{
    private readonly IdentityDbContext _dbContext;

    public UpdateUserStatusCommandHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync([request.UserId], cancellationToken);
        if (user == null)
        {
            return Result.Failure(Error.NotFound("Identity.UserNotFound", "Không tìm thấy người dùng."));
        }

        user.SetStatus(request.IsActive);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (!request.IsActive)
            {
                await _dbContext.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAtUtc, DateTimeOffset.UtcNow), cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict("Identity.UserChanged", "Tài khoản vừa được cập nhật. Vui lòng thử lại."));
        }

        return Result.Success();
    }
}
