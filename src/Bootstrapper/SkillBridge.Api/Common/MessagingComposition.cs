using Microsoft.EntityFrameworkCore;
using SkillBridge.Modules.Identity.Infrastructure.Data;
using SkillBridge.Modules.Messaging.Application;

namespace SkillBridge.Api.Common;

public static class MessagingComposition
{
    public static IServiceCollection AddMessagingComposition(this IServiceCollection services) =>
        services.AddScoped<IMessagingUserDirectory, IdentityMessagingUserDirectory>();
}

internal sealed class IdentityMessagingUserDirectory(IdentityDbContext db) : IMessagingUserDirectory
{
    public Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);

    public Task<bool> IsSessionValidAsync(Guid userId, string securityStamp, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive && x.SecurityStamp == securityStamp, cancellationToken);
}
