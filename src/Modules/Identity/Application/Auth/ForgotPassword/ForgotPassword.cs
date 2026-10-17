using System.Security.Cryptography;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Infrastructure.Persistence;

namespace SkillBridge.Modules.Identity.Application.Auth.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : ICommand;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256)
            .Must(email => email is null || !email.Contains('\0'));
    }
}

public sealed class ForgotPasswordCommandHandler(
    IdentityDbContext dbContext,
    IPasswordResetEmailSender emailSender,
    ILogger<ForgotPasswordCommandHandler> logger) : ICommandHandler<ForgotPasswordCommand>
{
    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        if (!emailSender.IsAvailable)
            return Result.Failure(Error.Failure("Identity.PasswordRecoveryUnavailable", "Dịch vụ khôi phục mật khẩu chưa được cấu hình."));

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        string token;
        User user;
        PasswordResetToken reset;
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var account = await dbContext.Users.FromSqlInterpolated(
                $"SELECT * FROM identity.users WHERE \"NormalizedEmail\" = {normalizedEmail} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (account is null || !account.IsActive) return Result.Success();
            user = account;
            var now = DateTimeOffset.UtcNow;
            var existing = await dbContext.PasswordResetTokens.SingleOrDefaultAsync(value => value.UserId == user.Id, cancellationToken);
            if (existing is not null && existing.CreatedAtUtc > now.AddMinutes(-1)) return Result.Success();

            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            reset = existing ?? PasswordResetToken.Create(user.Id, token, user.SecurityStamp, now);
            if (existing is null) dbContext.PasswordResetTokens.Add(reset);
            else reset.Replace(token, user.SecurityStamp, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // Provider failures must not reveal whether an account exists; never log the provider exception or token.
        try
        {
            if (!await emailSender.SendAsync(user.Email, token, reset.ExpiresAtUtc, cancellationToken))
                logger.LogWarning("Password recovery email delivery failed.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Password recovery email delivery failed.");
        }
        return Result.Success();
    }
}
