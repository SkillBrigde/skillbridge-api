using Microsoft.Extensions.Hosting;
using SkillBridge.Modules.Identity.Application;

namespace SkillBridge.Modules.Identity.Infrastructure;

// Development-only mailbox. No HTTP endpoint or log exposes these tokens.
public sealed class DevelopmentPasswordResetEmailSender(IHostEnvironment environment) : IPasswordResetEmailSender
{
    private readonly Dictionary<string, PasswordResetEmail> _mailbox = new(StringComparer.OrdinalIgnoreCase);
    public bool IsAvailable => environment.IsDevelopment();

    public Task<bool> SendAsync(string email, string token, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAvailable) return Task.FromResult(false);
        lock (_mailbox)
        {
            foreach (var expired in _mailbox.Where(pair => pair.Value.ExpiresAtUtc <= DateTimeOffset.UtcNow).Select(pair => pair.Key).ToArray())
                _mailbox.Remove(expired);
            if (_mailbox.Count >= 1000 && !_mailbox.ContainsKey(email)) return Task.FromResult(false);
            _mailbox[email] = new PasswordResetEmail(token, expiresAtUtc);
        }
        return Task.FromResult(true);
    }

    public PasswordResetEmail? Take(string email)
    {
        if (!IsAvailable) return null;
        lock (_mailbox)
            return _mailbox.Remove(email, out var message) && message.ExpiresAtUtc > DateTimeOffset.UtcNow ? message : null;
    }
}

public sealed record PasswordResetEmail(string Token, DateTimeOffset ExpiresAtUtc);
