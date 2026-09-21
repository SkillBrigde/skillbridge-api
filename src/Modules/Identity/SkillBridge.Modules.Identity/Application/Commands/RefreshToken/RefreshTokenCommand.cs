using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Application.DTOs;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity.Application.Commands.RefreshToken;

public sealed record RefreshTokenCommand(
    string RefreshToken,
    string? IpAddress = null
) : ICommand<AuthResponse>;

public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly JwtSettings _jwtSettings;

    public RefreshTokenCommandHandler(
        IdentityDbContext dbContext,
        IJwtTokenGenerator jwtTokenGenerator,
        JwtSettings jwtSettings)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _jwtSettings = jwtSettings;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result<AuthResponse>.Failure(Error.Validation(
                "Identity.InvalidRefreshToken",
                "Refresh token không được để trống."));
        }

        var existingToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, cancellationToken);

        if (existingToken == null || !existingToken.IsActive)
        {
            return Result<AuthResponse>.Failure(Error.Validation(
                "Identity.InvalidRefreshToken",
                "Refresh token không hợp lệ, đã bị thu hồi hoặc đã hết hạn."));
        }

        var user = await _dbContext.Users.FindAsync([existingToken.UserId], cancellationToken);
        if (user == null || !user.IsActive)
        {
            return Result<AuthResponse>.Failure(Error.NotFound(
                "Identity.UserNotFound",
                "Tài khoản người dùng không tồn tại hoặc đã bị khóa."));
        }

        // Áp dụng cơ chế Token Rotation: Thu hồi token cũ và sinh token mới
        var newRawRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        existingToken.Revoke(replacedByToken: newRawRefreshToken);

        var newRefreshToken = Domain.RefreshToken.Create(
            userId: user.Id,
            token: newRawRefreshToken,
            expiresAtUtc: DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            createdByIp: request.IpAddress
        );

        _dbContext.RefreshTokens.Add(newRefreshToken);

        var newAccessToken = _jwtTokenGenerator.GenerateAccessToken(user.Id, user.Email, user.FullName, [user.Role]);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new AuthResponse(
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role,
            AccessToken: newAccessToken,
            RefreshToken: newRawRefreshToken,
            ExpiresIn: _jwtSettings.AccessTokenExpirationMinutes * 60
        );

        return Result<AuthResponse>.Success(response);
    }
}
