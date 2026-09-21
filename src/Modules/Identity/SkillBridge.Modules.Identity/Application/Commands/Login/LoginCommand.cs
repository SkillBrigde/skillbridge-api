using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Application.DTOs;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity.Application.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    string? IpAddress = null
) : ICommand<AuthResponse>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.");
    }
}

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, AuthResponse>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly JwtSettings _jwtSettings;

    public LoginCommandHandler(
        IdentityDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        JwtSettings jwtSettings)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _jwtSettings = jwtSettings;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user == null)
        {
            return Result<AuthResponse>.Failure(Error.NotFound(
                "Identity.InvalidCredentials",
                "Email hoặc mật khẩu không chính xác."));
        }

        if (user.IsLockedOut)
        {
            return Result<AuthResponse>.Failure(Error.Failure(
                "Identity.AccountLocked",
                $"Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau {user.LockoutEndUtc:HH:mm:ss} UTC."));
        }

        if (!user.IsActive)
        {
            return Result<AuthResponse>.Failure(Error.Failure(
                "Identity.AccountDisabled",
                "Tài khoản của bạn đã bị vô hiệu hóa bởi ban quản trị."));
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<AuthResponse>.Failure(Error.NotFound(
                "Identity.InvalidCredentials",
                "Email hoặc mật khẩu không chính xác."));
        }

        // Đăng nhập thành công -> Reset số lần đăng nhập sai
        user.ResetFailedLogin();

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user.Id, user.Email, user.FullName, [user.Role]);
        var rawRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var refreshToken = Domain.RefreshToken.Create(
            userId: user.Id,
            token: rawRefreshToken,
            expiresAtUtc: DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            createdByIp: request.IpAddress
        );

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
