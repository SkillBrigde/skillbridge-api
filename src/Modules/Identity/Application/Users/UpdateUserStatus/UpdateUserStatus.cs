using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Infrastructure.Persistence;

namespace SkillBridge.Modules.Identity.Application.Users.UpdateUserStatus;

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
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
