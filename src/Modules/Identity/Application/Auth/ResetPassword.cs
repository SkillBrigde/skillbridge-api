using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Infrastructure.Persistence;

namespace SkillBridge.Modules.Identity.Application.Auth;

public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : ICommand;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256)
            .Must(email => email is null || !email.Contains('\0'));
        RuleFor(request => request.Token).NotEmpty().Matches("^[0-9A-F]{64}$");
        RuleFor(request => request.NewPassword).NotEmpty().MinimumLength(8)
            .Must(password => password is null || Encoding.UTF8.GetByteCount(password) <= 72)
            .WithMessage("Mật khẩu mới không được vượt quá 72 byte UTF-8.")
            .Matches(@"[A-Z]").Matches(@"[a-z]").Matches(@"[0-9]").Matches(@"[^\p{L}\p{N}\s]");
    }
}

public sealed class ResetPasswordCommandHandler(IdentityDbContext dbContext, IPasswordHasher passwordHasher)
    : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var invalid = Error.Validation("Identity.InvalidResetToken", "Mã đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users.FromSqlInterpolated(
                $"SELECT * FROM identity.users WHERE \"NormalizedEmail\" = {normalizedEmail} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || !user.IsActive) return Result.Failure(invalid);

        var reset = await dbContext.PasswordResetTokens.SingleOrDefaultAsync(value => value.UserId == user.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (reset is null || !reset.CanUse(Domain.RefreshToken.Hash(request.Token), user.SecurityStamp, now))
            return Result.Failure(invalid);

        user.ChangePassword(passwordHasher.HashPassword(request.NewPassword));
        user.ResetFailedLogin();
        reset.Consume(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.RefreshTokens.Where(value => value.UserId == user.Id && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.RevokedAtUtc, now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
