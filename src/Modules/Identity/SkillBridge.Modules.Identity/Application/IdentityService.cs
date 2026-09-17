using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Domain;

namespace SkillBridge.Modules.Identity.Application;

public sealed class IdentityService(
    IIdentityData data, IPasswordHasher<User> hasher, ITokenIssuer tokens, TimeProvider clock)
{
    public async Task<Result<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var password = User.ValidatePassword(request.Password);
        if (password.IsFailure) return Result.Failure<UserResponse>(password.Error);
        var creation = User.Create(request.Email, request.FullName, request.Role, clock.GetUtcNow());
        if (creation.IsFailure) return Result.Failure<UserResponse>(creation.Error);
        var user = creation.Value;
        if (await data.Users.AnyAsync(x => x.Email == user.Email, ct))
            return Result.Failure<UserResponse>(IdentityErrors.DuplicateEmail);
        user.SetPasswordHash(hasher.HashPassword(user, request.Password!), clock.GetUtcNow());
        data.Users.Add(user);
        var registered = new UserRegisteredIntegrationEvent(Guid.CreateVersion7(), clock.GetUtcNow(),
            user.Id, user.Email, user.FullName, user.Roles);
        data.OutboxMessages.Add(new OutboxMessage
        {
            Id = registered.EventId,
            Type = nameof(UserRegisteredIntegrationEvent),
            Content = JsonSerializer.Serialize(registered),
            OccurredOnUtc = registered.OccurredOnUtc
        });
        var saved = await data.SaveAsync(ct);
        return saved.IsSuccess ? UserResponse.From(user) : Result.Failure<UserResponse>(saved.Error);
    }

    public async Task<Result<AuthenticationResponse>> LoginAsync(
        LoginRequest request, string userAgent, string ipAddress, CancellationToken ct)
    {
        if (!User.IsValidEmail(request.Email) || request.Password is not { Length: > 0 and <= 128 })
            return Result.Failure<AuthenticationResponse>(Error.Validation("Identity.Login", "Email và mật khẩu không hợp lệ."));
        var email = User.NormalizeEmail(request.Email!);
        var userId = await data.Users.Where(x => x.Email == email).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (userId is null) return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidCredentials);

        // All session mutations acquire the user row first: refresh, login, password and admin lock.
        await using var transaction = await data.LockUserAsync(userId.Value, ct);
        var user = await data.Users.SingleAsync(x => x.Id == userId, ct);
        var now = clock.GetUtcNow();
        if (!user.IsActive) return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidCredentials);
        if (user.IsLockedOut(now)) return Result.Failure<AuthenticationResponse>(IdentityErrors.Locked);
        var verification = user.PasswordHash is null ? PasswordVerificationResult.Failed :
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.RecordFailedLogin(now);
            var saved = await data.SaveAsync(ct);
            if (saved.IsFailure) return Result.Failure<AuthenticationResponse>(saved.Error);
            await transaction.CommitAsync(ct);
            return Result.Failure<AuthenticationResponse>(user.IsLockedOut(now) ? IdentityErrors.Locked : IdentityErrors.InvalidCredentials);
        }
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.SetPasswordHash(hasher.HashPassword(user, request.Password), now);
        user.RecordSuccessfulLogin();
        var session = UserSession.Create(user.Id, userAgent, ipAddress, now);
        data.Sessions.Add(session);
        var response = IssueTokens(user, session);
        var result = await data.SaveAsync(ct);
        if (result.IsFailure) return Result.Failure<AuthenticationResponse>(result.Error);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<Result<AuthenticationResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        if (request.SessionId == Guid.Empty || request.RefreshToken is not { Length: 64 })
            return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidToken);
        var ownerId = await data.Sessions.Where(x => x.Id == request.SessionId)
            .Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct);
        if (ownerId is null) return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidToken);
        await using var transaction = await data.LockUserAsync(ownerId.Value, ct);
        var session = await data.Sessions.SingleAsync(x => x.Id == request.SessionId, ct);
        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var oldToken = await data.RefreshTokens.SingleOrDefaultAsync(
            x => x.SessionId == session.Id && x.TokenHash == hash, ct);
        var user = await data.Users.SingleAsync(x => x.Id == session.UserId, ct);
        var now = clock.GetUtcNow();
        if (oldToken is null) return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidToken);
        if (oldToken.IsRevoked)
        {
            await RevokeSessionAsync(session, ct);
            var saved = await data.SaveAsync(ct);
            if (saved.IsFailure) return Result.Failure<AuthenticationResponse>(saved.Error);
            await transaction.CommitAsync(ct);
            return Result.Failure<AuthenticationResponse>(IdentityErrors.Replay);
        }
        if (!session.IsValid(now) || oldToken.ExpiresAtUtc <= now || !user.IsActive || user.IsLockedOut(now))
            return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidToken);
        session.Touch(now);
        var rawToken = tokens.CreateRefreshToken();
        var replacement = RefreshToken.Create(session.Id, tokens.HashRefreshToken(rawToken), session.ExpiresAtUtc);
        oldToken.Revoke(replacement.Id);
        data.RefreshTokens.Add(replacement);
        var response = new AuthenticationResponse(tokens.CreateAccessToken(user, session), rawToken, 900,
            session.Id, UserResponse.From(user));
        var result = await data.SaveAsync(ct);
        if (result.IsFailure) return Result.Failure<AuthenticationResponse>(result.Error);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<Result<UserResponse>> GetUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await data.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        return user is null ? Result.Failure<UserResponse>(IdentityErrors.UserNotFound) : UserResponse.From(user);
    }

    public async Task<Result<UserResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        await using var transaction = await data.LockUserAsync(userId, ct);
        var user = await data.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return Result.Failure<UserResponse>(IdentityErrors.UserNotFound);
        var updated = user.UpdateProfile(request.FullName, request.AvatarUrl, clock.GetUtcNow());
        if (updated.IsFailure) return Result.Failure<UserResponse>(updated.Error);
        var saved = await data.SaveAsync(ct);
        if (saved.IsFailure) return Result.Failure<UserResponse>(saved.Error);
        await transaction.CommitAsync(ct);
        return UserResponse.From(user);
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var validation = User.ValidatePassword(request.NewPassword);
        if (validation.IsFailure) return validation;
        if (request.NewPassword != request.ConfirmNewPassword)
            return Result.Failure(Error.Validation("User.ConfirmPassword", "Mật khẩu xác nhận không khớp."));
        if (request.CurrentPassword is not { Length: > 0 and <= 128 })
            return Result.Failure(IdentityErrors.InvalidCredentials);
        await using var transaction = await data.LockUserAsync(userId, ct);
        var user = await data.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user?.PasswordHash is null || hasher.VerifyHashedPassword(user, user.PasswordHash,
            request.CurrentPassword) == PasswordVerificationResult.Failed)
            return Result.Failure(IdentityErrors.InvalidCredentials);
        user.SetPasswordHash(hasher.HashPassword(user, request.NewPassword!), clock.GetUtcNow());
        foreach (var session in await data.Sessions.Where(x => x.UserId == userId && x.IsActive).ToListAsync(ct))
            await RevokeSessionAsync(session, ct);
        var saved = await data.SaveAsync(ct);
        if (saved.IsFailure) return saved;
        await transaction.CommitAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<SessionResponse>>> GetSessionsAsync(Guid userId, Guid currentSessionId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var sessions = await data.Sessions.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.LastActiveAtUtc)
            .Select(x => new SessionResponse(x.Id, x.UserAgent, x.IpAddress, x.LastActiveAtUtc, x.Id == currentSessionId))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<SessionResponse>>(sessions);
    }

    public async Task<Result<int>> RevokeSessionsAsync(Guid userId, Guid currentSessionId, Guid? targetSessionId, CancellationToken ct)
    {
        await using var transaction = await data.LockUserAsync(userId, ct);
        var query = data.Sessions.Where(x => x.UserId == userId);
        query = targetSessionId.HasValue ? query.Where(x => x.Id == targetSessionId.Value) :
            query.Where(x => x.Id != currentSessionId && x.IsActive);
        var sessions = await query.ToListAsync(ct);
        if (targetSessionId.HasValue && sessions.Count == 0)
            return Result.Failure<int>(IdentityErrors.SessionNotFound);
        foreach (var session in sessions) await RevokeSessionAsync(session, ct);
        var saved = await data.SaveAsync(ct);
        if (saved.IsFailure) return Result.Failure<int>(saved.Error);
        await transaction.CommitAsync(ct);
        return sessions.Count;
    }

    public async Task<Result<UserPage>> GetUsersAsync(string? search, string? role, string? status, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue ||
            search?.Length > 200 || (status is not null && status is not ("Active" or "Locked")))
            return Result.Failure<UserPage>(Error.Validation("Users.Filter", "Bộ lọc hoặc phân trang không hợp lệ."));
        var query = data.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Email.Contains(term) || x.FullName.ToLower().Contains(term));
        }
        if (!string.IsNullOrEmpty(role)) query = query.Where(x => x.Roles.Contains(role));
        if (status is not null) query = query.Where(x => x.IsActive == (status == "Active"));
        var total = await query.LongCountAsync(ct);
        var users = await query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new UserPage(users.Select(UserResponse.From).ToList(), total, page, pageSize);
    }

    public async Task<Result<UserResponse>> SetStatusAsync(Guid actorId, Guid userId, SetStatusRequest request, CancellationToken ct)
    {
        if (actorId == userId && request.Status == "Locked")
            return Result.Failure<UserResponse>(Error.Forbidden("User.SelfLock", "Không thể tự khóa tài khoản."));
        if (request.LockReason?.Length > 500)
            return Result.Failure<UserResponse>(Error.Validation("User.LockReason", "Lý do khóa tối đa 500 ký tự."));
        await using var transaction = await data.LockUserAsync(userId, ct);
        var user = await data.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return Result.Failure<UserResponse>(IdentityErrors.UserNotFound);
        var changed = user.SetStatus(request.Status, clock.GetUtcNow());
        if (changed.IsFailure) return Result.Failure<UserResponse>(changed.Error);
        data.SecurityAuditEntries.Add(SecurityAuditEntry.Create(actorId, userId,
            $"User.Status.{request.Status}", request.LockReason, clock.GetUtcNow()));
        foreach (var session in await data.Sessions.Where(x => x.UserId == userId && x.IsActive).ToListAsync(ct))
            await RevokeSessionAsync(session, ct);
        var saved = await data.SaveAsync(ct);
        if (saved.IsFailure) return Result.Failure<UserResponse>(saved.Error);
        await transaction.CommitAsync(ct);
        return UserResponse.From(user);
    }

    private AuthenticationResponse IssueTokens(User user, UserSession session)
    {
        var rawToken = tokens.CreateRefreshToken();
        data.RefreshTokens.Add(RefreshToken.Create(session.Id, tokens.HashRefreshToken(rawToken), session.ExpiresAtUtc));
        return new AuthenticationResponse(tokens.CreateAccessToken(user, session), rawToken, 900, session.Id, UserResponse.From(user));
    }

    private async Task RevokeSessionAsync(UserSession session, CancellationToken ct)
    {
        session.Revoke();
        foreach (var token in await data.RefreshTokens.Where(x => x.SessionId == session.Id && !x.IsRevoked).ToListAsync(ct))
            token.Revoke();
    }
}
