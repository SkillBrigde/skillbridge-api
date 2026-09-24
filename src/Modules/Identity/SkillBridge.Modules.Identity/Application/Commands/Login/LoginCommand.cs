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
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(256)
            .Must(email => email is null || !email.Contains('\0'));

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
            return Result<AuthResponse>.Failure(Error.Unauthorized(
                "Identity.InvalidCredentials",
                "Email hoặc mật khẩu không chính xác."));
        }

        if (user.IsLockedOut)
        {
            return Result<AuthResponse>.Failure(Error.Unauthorized(
                "Identity.InvalidCredentials", "Email hoặc mật khẩu không chính xác."));
        }

        if (!user.IsActive)
        {
            return Result<AuthResponse>.Failure(Error.Unauthorized(
                "Identity.InvalidCredentials", "Email hoặc mật khẩu không chính xác."));
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            var now = DateTimeOffset.UtcNow;
            await _dbContext.Users
                .Where(u => u.Id == user.Id && u.IsActive && u.SecurityStamp == user.SecurityStamp &&
                    u.LockoutEnabled && (u.LockoutEndUtc == null || u.LockoutEndUtc <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.AccessFailedCount,
                        u => u.LockoutEndUtc != null ? 1 : u.AccessFailedCount + 1)
                    .SetProperty(u => u.LockoutEndUtc,
                        u => u.LockoutEndUtc == null && u.AccessFailedCount + 1 >= 5
                            ? now.AddMinutes(15) : (DateTimeOffset?)null)
                    .SetProperty(u => u.UpdatedAtUtc, now), cancellationToken);

            return Result<AuthResponse>.Failure(Error.Unauthorized(
                "Identity.InvalidCredentials",
                "Email hoặc mật khẩu không chính xác."));
        }

        // Recheck account state atomically after password verification; concurrent failures may have locked it.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var loginTime = DateTimeOffset.UtcNow;
        var updated = await _dbContext.Users
            .Where(u => u.Id == user.Id && u.IsActive && u.SecurityStamp == user.SecurityStamp &&
                (u.LockoutEndUtc == null || u.LockoutEndUtc <= loginTime))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.AccessFailedCount, 0)
                .SetProperty(u => u.LockoutEndUtc, (DateTimeOffset?)null)
                .SetProperty(u => u.UpdatedAtUtc, loginTime), cancellationToken);
        if (updated == 0)
        {
            return Result<AuthResponse>.Failure(Error.Unauthorized(
                "Identity.InvalidCredentials", "Email hoặc mật khẩu không chính xác."));
        }

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user.Id, user.Email, user.FullName, [user.Role], user.SecurityStamp);
        var rawRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var refreshToken = Domain.RefreshToken.Create(
            userId: user.Id,
            token: rawRefreshToken,
            expiresAtUtc: DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            securityStamp: user.SecurityStamp,
            createdByIp: request.IpAddress
        );

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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
