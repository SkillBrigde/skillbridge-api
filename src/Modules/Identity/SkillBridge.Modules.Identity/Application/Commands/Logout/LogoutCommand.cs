using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity.Application.Commands.Logout;

public sealed record LogoutCommand(string? RefreshToken) : ICommand;

public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand>
{
    private readonly IdentityDbContext _dbContext;

    public LogoutCommandHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Success();
        }

        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, cancellationToken);

        if (token != null && token.IsActive)
        {
            token.Revoke();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
