using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity.Application.Commands.UpdateCurrentUser;

public sealed record UpdateCurrentUserCommand(Guid UserId, string FullName) : ICommand;

public sealed class UpdateCurrentUserCommandValidator : AbstractValidator<UpdateCurrentUserCommand>
{
    public UpdateCurrentUserCommandValidator() => RuleFor(request => request.FullName).NotEmpty().MaximumLength(200)
        .Must(name => name is null || !name.Contains('\0')).WithMessage("Họ và tên không được chứa ký tự NUL.");
}

public sealed class UpdateCurrentUserCommandHandler(IdentityDbContext dbContext) : ICommandHandler<UpdateCurrentUserCommand>
{
    public async Task<Result> Handle(UpdateCurrentUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync([request.UserId], cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure(Error.Unauthorized("Identity.UserNotFound", "Không tìm thấy người dùng đang hoạt động."));
        }

        user.UpdateProfile(request.FullName, user.PhoneNumber, user.AvatarUrl);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict("Identity.UserChanged", "Tài khoản vừa được cập nhật. Vui lòng thử lại."));
        }
        return Result.Success();
    }
}
