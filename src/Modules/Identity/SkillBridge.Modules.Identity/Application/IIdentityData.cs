using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Domain;

namespace SkillBridge.Modules.Identity.Application;

// The application owns this persistence port; only Infrastructure implements locking and storage.
public interface IIdentityData
{
    DbSet<User> Users { get; }
    DbSet<UserSession> Sessions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<SecurityAuditEntry> SecurityAuditEntries { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    Task<IIdentityTransaction> LockUserAsync(Guid userId, CancellationToken ct);
    Task<Result> SaveAsync(CancellationToken ct);
}

public interface IIdentityTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}

public interface ITokenIssuer
{
    string CreateAccessToken(User user, UserSession session);
    string CreateRefreshToken();
    string HashRefreshToken(string token);
}
