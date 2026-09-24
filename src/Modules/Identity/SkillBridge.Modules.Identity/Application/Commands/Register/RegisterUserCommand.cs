using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Events;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity.Application.Commands.Register;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FullName,
    string Role,
    string? PhoneNumber = null
) : ICommand<Guid>;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(256).WithMessage("Email không được vượt quá 256 ký tự.")
            .Must(email => email is null || !email.Contains('\0'));

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
            .Must(password => password is null || Encoding.UTF8.GetByteCount(password) <= 72)
            .WithMessage("Mật khẩu không được vượt quá 72 byte UTF-8.")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ hoa.")
            .Matches(@"[a-z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ thường.")
            .Matches(@"[0-9]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ số.")
            .Matches(@"[^\p{L}\p{N}\s]").WithMessage("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(200).WithMessage("Họ và tên không được vượt quá 200 ký tự.")
            .Must(name => name is null || !name.Contains('\0'));

        RuleFor(x => x.Role)
            .Must(r => r is "Mentor" or "Mentee")
            .WithMessage("Vai trò chỉ có thể là Mentor hoặc Mentee.");

        RuleFor(x => x.PhoneNumber).MaximumLength(30)
            .Must(phone => phone is null || !phone.Contains('\0'));
    }
}

public sealed class RegisterUserCommandHandler(
    IdentityDbContext dbContext,
    IPasswordHasher passwordHasher) : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var emailExists = await dbContext.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return Result<Guid>.Failure(Error.Conflict(
                "Identity.EmailAlreadyExists",
                "Email này đã được đăng ký trên hệ thống."));
        }

        var passwordHash = passwordHasher.HashPassword(request.Password);
        var user = User.Create(
            email: request.Email,
            passwordHash: passwordHash,
            fullName: request.FullName,
            role: request.Role,
            phoneNumber: request.PhoneNumber
        );

        dbContext.Users.Add(user);
        var integrationEvent = new UserRegisteredIntegrationEvent(user.Id, user.Email, user.FullName, user.Role);
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = integrationEvent.EventId,
            Type = integrationEvent.EventType,
            Content = JsonSerializer.Serialize(integrationEvent),
            OccurredOnUtc = integrationEvent.OccurredOnUtc
        });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_Email" or "IX_users_NormalizedEmail" })
        {
            return Result<Guid>.Failure(Error.Conflict("Identity.EmailAlreadyExists", "Email này đã được đăng ký trên hệ thống."));
        }

        return Result<Guid>.Success(user.Id);
    }
}
