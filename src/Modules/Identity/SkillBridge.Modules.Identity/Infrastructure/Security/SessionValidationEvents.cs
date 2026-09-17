using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Modules.Identity.Infrastructure.Security;

public sealed class SessionValidationEvents(IdentityDbContext db, TimeProvider clock) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var userId) ||
            !Guid.TryParse(context.Principal?.FindFirstValue("sid"), out var sessionId) ||
            !Guid.TryParse(context.Principal?.FindFirstValue("security_stamp"), out var stamp))
        {
            context.Fail("Invalid identity claims.");
            return;
        }
        var now = clock.GetUtcNow();
        var valid = await (from session in db.Sessions.AsNoTracking()
                           join user in db.Users.AsNoTracking() on session.UserId equals user.Id
                           where session.Id == sessionId && user.Id == userId &&
                                 session.IsActive && session.ExpiresAtUtc > now && user.IsActive &&
                                 user.SecurityStamp == stamp
                           select session.Id).AnyAsync(context.HttpContext.RequestAborted);
        if (!valid) context.Fail("Session revoked or expired.");
    }
}
