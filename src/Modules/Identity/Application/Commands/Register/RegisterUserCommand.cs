using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Application.DTOs;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Events;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity.Application.Commands.Register;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FullName,
    string Role = "Mentee",
    string? PhoneNumber = null
) : ICommand<AuthResponse>;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(256).WithMessage("Email không được vượt quá 256 ký tự.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ hoa.")
            .Matches(@"[a-z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ thường.")
            .Matches(@"[0-9]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ số.")
            .Matches(@"[\!\?\*\@\#\$\%\^\&\+\=]").WithMessage("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(200).WithMessage("Họ và tên không được vượt quá 200 ký tự.");

        RuleFor(x => x.Role)
            .Must(r => r is "Mentor" or "Mentee")
            .WithMessage("Vai trò chỉ có thể là Mentor hoặc Mentee.");
    }
}

public sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, AuthResponse>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEventBus _eventBus;
    private readonly JwtSettings _jwtSettings;

    public RegisterUserCommandHandler(
        IdentityDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IEventBus eventBus,
        JwtSettings jwtSettings)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _eventBus = eventBus;
        _jwtSettings = jwtSettings;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var emailExists = await _dbContext.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return Result<AuthResponse>.Failure(Error.Conflict(
                "Identity.EmailAlreadyExists",
                "Email này đã được đăng ký trên hệ thống."));
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = User.Create(
            email: request.Email,
            passwordHash: passwordHash,
            fullName: request.FullName,
            role: request.Role,
            phoneNumber: request.PhoneNumber
        );

        _dbContext.Users.Add(user);

        // Tạo access token & refresh token
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user.Id, user.Email, user.FullName, [user.Role]);
        var rawRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var refreshToken = Domain.RefreshToken.Create(
            userId: user.Id,
            token: rawRefreshToken,
            expiresAtUtc: DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)
        );
        _dbContext.RefreshTokens.Add(refreshToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Bắn integration event để Profiles module tạo profile tự động
        await _eventBus.PublishAsync(
            new UserRegisteredIntegrationEvent(user.Id, user.Email, user.FullName, user.Role),
            cancellationToken
        );

        var response = new AuthResponse(
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role,
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            ExpiresIn: _jwtSettings.AccessTokenExpirationMinutes * 60
        );

        return Result<AuthResponse>.Success(response);
    }
}
