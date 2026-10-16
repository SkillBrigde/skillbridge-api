using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Application;
using SkillBridge.Modules.Identity.Application.Commands.ForgotPassword;
using SkillBridge.Modules.Identity.Application.Commands.ResetPassword;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Infrastructure;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Checks;

internal static class IdentityRecoveryChecks
{
    private const string Email = "recovery@checks.test";
    private const string NewPassword = "RecoveredPassword123!";
    private const string Prefix = "/api/v1/identity/auth/";

    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<RecoveryMailbox>();
        services.AddSingleton<IPasswordResetEmailSender>(provider => provider.GetRequiredService<RecoveryMailbox>());
    }

    public static async Task CheckDomainAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var raw = new string('A', 64);
        var token = PasswordResetToken.Create(Guid.NewGuid(), raw, "stamp", now);
        Program.Check(token.TokenHash != raw && token.TokenHash.Length == 64 && token.ExpiresAtUtc == now.AddMinutes(30), "Reset tokens must be hash-only and expire after 30 minutes.");
        Program.Check(token.CanUse(RefreshToken.Hash(raw), "stamp", now), "Fresh reset token must be usable.");
        Program.Check(!token.CanUse(RefreshToken.Hash(raw), "other", now) && !token.CanUse(RefreshToken.Hash(raw), "stamp", token.ExpiresAtUtc), "Reset tokens must bind to the security stamp and reject the expiry boundary.");
        token.Consume(now);
        Program.Check(!token.CanUse(RefreshToken.Hash(raw), "stamp", now), "Consumed reset tokens must be rejected.");
        var validator = new ResetPasswordCommandValidator();
        Program.Check(!validator.Validate(new ResetPasswordCommand(Email, raw, "weak")).IsValid &&
            !validator.Validate(new ResetPasswordCommand(Email, raw, "Ab1!" + new string('đ', 35))).IsValid &&
            !validator.Validate(new ResetPasswordCommand(Email, new string('A', 65), NewPassword)).IsValid &&
            !new ForgotPasswordCommandValidator().Validate(new ForgotPasswordCommand("bad\0@checks.test")).IsValid, "Recovery validation must reject weak/oversized passwords, oversized tokens and NUL emails.");
        var production = new DevelopmentPasswordResetEmailSender(new TestEnvironment { EnvironmentName = Environments.Production });
        Program.Check(!production.IsAvailable && !await production.SendAsync(Email, raw, now.AddMinutes(30), default) && production.Take(Email) is null,
            "Development email adapter must fail closed outside Development.");
    }

    public static async Task RunAsync(HttpClient client, IServiceProvider services)
    {
        var mailbox = services.GetRequiredService<RecoveryMailbox>();
        Guid userId;
        string stamp;
        string oldAccessToken;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var user = User.Create(Email, hasher.HashPassword("Password123!"), "Recovery checks");
            var inactive = User.Create("inactive-recovery@checks.test", hasher.HashPassword("Password123!"), "Inactive");
            inactive.SetStatus(false);
            user.RecordFailedLogin();
            userId = user.Id;
            stamp = user.SecurityStamp;
            db.Users.AddRange(user, inactive);
            db.RefreshTokens.Add(RefreshToken.Create(user.Id, "recovery-check-refresh", DateTimeOffset.UtcNow.AddDays(1), stamp));
            await db.SaveChangesAsync();
            oldAccessToken = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>()
                .GenerateAccessToken(user.Id, user.Email, user.FullName, [user.Role], stamp);
        }

        await ExpectAsync(client, "forgot-password", new { email = "absent-recovery@checks.test" }, 204);
        await ExpectAsync(client, "forgot-password", new { email = "inactive-recovery@checks.test" }, 204);
        await ExpectAsync(client, "forgot-password", new { email = Email.ToUpperInvariant() }, 204);
        var first = mailbox.Take(Email);
        Program.Check(mailbox.Delivered == 1 && first is not null, "Only active accounts should receive recovery email; responses must remain identical.");
        await ExpectAsync(client, "forgot-password", new { email = Email }, 204);
        Program.Check(mailbox.Delivered == 1, "Per-account recovery cooldown must suppress duplicate email.");
        await ExpectAsync(client, "reset-password", new { email = "absent-recovery@checks.test", token = first!.Token, newPassword = NewPassword }, 400);
        await ExpectAsync(client, "reset-password", new { email = Email, token = first.Token, newPassword = "weak" }, 400);

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var reset = await db.PasswordResetTokens.SingleAsync(value => value.UserId == userId);
            Program.Check(reset.TokenHash == RefreshToken.Hash(first.Token) && reset.TokenHash != first.Token, "Database must contain the token hash, never the emailed value.");
            await db.PasswordResetTokens.Where(value => value.UserId == userId).ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.ExpiresAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1))
                .SetProperty(value => value.CreatedAtUtc, DateTimeOffset.UtcNow.AddMinutes(-2)));
        }
        await ExpectAsync(client, "reset-password", new { email = Email, token = first.Token, newPassword = NewPassword }, 400);
        await ExpectAsync(client, "forgot-password", new { email = Email }, 204);
        var second = mailbox.Take(Email)!;
        Program.Check(second.Token != first.Token, "Replacement reset tokens must be freshly randomized.");
        await ExpectAsync(client, "reset-password", new { email = Email, token = first.Token, newPassword = NewPassword }, 400);

        // Both requests contend for the same user row: only one may consume the token.
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync(Prefix + "reset-password",
            new { email = Email, token = second.Token, newPassword = NewPassword })));
        try
        {
            Program.Check(results.Count(response => response.StatusCode == HttpStatusCode.NoContent) == 1 &&
                results.Count(response => response.StatusCode == HttpStatusCode.BadRequest) == 1,
                "Concurrent reset must have exactly one winner and reject the duplicate.");
        }
        finally { foreach (var response in results) response.Dispose(); }
        await ExpectAsync(client, "reset-password", new { email = Email, token = second.Token, newPassword = NewPassword }, 400);
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/identity/users/me"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oldAccessToken);
            using var response = await client.SendAsync(request);
            Program.Check(response.StatusCode == HttpStatusCode.Unauthorized, "Password reset must immediately invalidate existing JWTs.");
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = await db.Users.SingleAsync(value => value.Id == userId);
            Program.Check(user.SecurityStamp != stamp && user.AccessFailedCount == 0 &&
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>().VerifyPassword(NewPassword, user.PasswordHash), "Reset must update password/stamp and clear failed login state.");
            Program.Check(!await db.RefreshTokens.AnyAsync(value => value.UserId == userId && value.RevokedAtUtc == null) &&
                await db.PasswordResetTokens.AnyAsync(value => value.UserId == userId && value.ConsumedAtUtc != null), "Reset consumption and session revocation must persist together.");
        }

        mailbox.IsAvailable = false;
        await ExpectAsync(client, "forgot-password", new { email = Email }, 503);
        await ExpectAsync(client, "forgot-password", new { email = "absent-recovery@checks.test" }, 503);
        mailbox.IsAvailable = true;
        // A provider exception must not escape into logs or turn existing-email responses into 500.
        mailbox.FailDelivery = true;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.PasswordResetTokens.Where(value => value.UserId == userId).ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.CreatedAtUtc, DateTimeOffset.UtcNow.AddMinutes(-2)));
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ForgotPasswordCommand(Email));
            Program.Check(result.IsSuccess, "Mail transport errors must not enumerate existing addresses.");
        }
        mailbox.FailDelivery = false;
        Console.WriteLine("PASS: password recovery, non-enumerating responses, hashed tokens, expiry, one-use concurrency, revocation and unavailable email provider.");
    }

    private static async Task ExpectAsync(HttpClient client, string action, object body, int expected)
    {
        using var response = await client.PostAsJsonAsync(Prefix + action, body);
        Program.Check((int)response.StatusCode == expected, $"Recovery {action} expected {expected}, got {(int)response.StatusCode}.");
        if (expected == 204) Program.Check(string.IsNullOrEmpty(await response.Content.ReadAsStringAsync()), "Recovery success responses must not reveal tokens or account existence.");
    }

    private sealed class RecoveryMailbox : IPasswordResetEmailSender
    {
        private readonly ConcurrentDictionary<string, PasswordResetEmail> _messages = new(StringComparer.OrdinalIgnoreCase);
        public bool IsAvailable { get; set; } = true;
        public bool FailDelivery { get; set; }
        public int Delivered { get; private set; }
        public Task<bool> SendAsync(string email, string token, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
        {
            if (FailDelivery) throw new InvalidOperationException("Simulated transport failure.");
            _messages[email] = new PasswordResetEmail(token, expiresAtUtc);
            Delivered++;
            return Task.FromResult(true);
        }
        public PasswordResetEmail? Take(string email) => _messages.TryRemove(email, out var message) ? message : null;
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SkillBridge.Checks";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
