namespace SkillBridge.Modules.Identity.Application;

public interface IPasswordResetEmailSender
{
    bool IsAvailable { get; }
    Task<bool> SendAsync(string email, string token, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken);
}
