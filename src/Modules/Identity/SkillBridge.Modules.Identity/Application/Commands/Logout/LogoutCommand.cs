using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity.Application.Commands.Logout;

public sealed record LogoutCommand(Guid UserId, string? RefreshToken) : ICommand;

public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand>
{
    private readonly IdentityDbContext _dbContext;

    public LogoutCommandHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 512)
        {
            return Result.Success();
        }

        var tokenHash = Domain.RefreshToken.Hash(request.RefreshToken);
        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.UserId == request.UserId && t.Token == tokenHash, cancellationToken);
        if (token is not null)
        {
            await _dbContext.RevokeSessionsAsync(request.UserId, token.SecurityStamp, cancellationToken);
        }

        return Result.Success();
    }
}
