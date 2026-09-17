using FluentValidation;
using SkillBridge.BuildingBlocks.Behaviors;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Domain;

namespace SkillBridge.Tests;

public sealed class IdentityDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null, "Name", "Mentee")]
    [InlineData("bad email", "Name", "Mentee")]
    [InlineData("x@example.com", "", "Mentor")]
    [InlineData("x@example.com", "Name", "Admin")]
    [InlineData("x@example.com", "Name", "SuperAdmin")]
    public void Registration_rejects_invalid_fields_and_privilege_escalation(string? email, string name, string role)
        => Assert.True(User.Create(email, name, role, Now).IsFailure);

    [Fact]
    public void Registration_normalizes_email_and_creates_version7_id()
    {
        var user = User.Create("  USER@example.com ", " Name ", "Mentee", Now).Value;
        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("Name", user.FullName);
        Assert.Equal(7, user.Id.Version);
        Assert.Equal(["Mentee"], user.Roles);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("PASSWORD123!")]
    [InlineData("password123!")]
    [InlineData("Password!!")]
    [InlineData("Password123")]
    public void Weak_passwords_are_rejected(string? password) => Assert.True(User.ValidatePassword(password).IsFailure);

    [Fact]
    public void Lockout_expires_and_failed_count_restarts()
    {
        var user = User.Create("x@example.com", "Name", "Mentee", Now).Value;
        for (var i = 0; i < 4; i++) user.RecordFailedLogin(Now);
        Assert.False(user.IsLockedOut(Now));
        user.RecordFailedLogin(Now);
        Assert.True(user.IsLockedOut(Now.AddMinutes(14)));
        Assert.False(user.IsLockedOut(Now.AddMinutes(15)));
        user.RecordFailedLogin(Now.AddMinutes(15));
        Assert.Equal(1, user.AccessFailedCount);
        Assert.Null(user.LockoutEndUtc);
        user.RecordSuccessfulLogin();
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public void Password_and_status_changes_invalidate_security_stamp()
    {
        var user = User.Create("x@example.com", "Name", "Mentor", Now).Value;
        var initial = user.SecurityStamp;
        user.SetPasswordHash("hash", Now);
        Assert.NotEqual(initial, user.SecurityStamp);
        var changed = user.SecurityStamp;
        Assert.True(user.SetStatus("Locked", Now).IsSuccess);
        Assert.False(user.IsActive);
        Assert.NotEqual(changed, user.SecurityStamp);
        Assert.True(user.SetStatus("Unknown", Now).IsFailure);
    }

    [Fact]
    public void Invalid_profile_update_does_not_partially_mutate_user()
    {
        var user = User.Create("x@example.com", "Original", "Mentor", Now).Value;
        Assert.True(user.UpdateProfile("Changed", "javascript:alert(1)", Now).IsFailure);
        Assert.Equal("Original", user.FullName);
    }

    [Fact]
    public void Sessions_expire_at_boundary_and_revoke_immediately()
    {
        var session = UserSession.Create(Guid.NewGuid(), "browser", "127.0.0.1", Now);
        Assert.True(session.IsValid(Now.AddDays(7).AddTicks(-1)));
        Assert.False(session.IsValid(Now.AddDays(7)));
        session.Revoke();
        Assert.False(session.IsValid(Now));
    }

    [Fact]
    public async Task Validation_pipeline_returns_failure_without_calling_handler()
    {
        var validator = new InlineValidator<string>();
        validator.RuleFor(x => x).NotEmpty();
        var behavior = new ValidationBehavior<string, Result<Guid>>([validator]);
        var called = false;
        var result = await behavior.Handle("", _ =>
        {
            called = true;
            return Task.FromResult(Result.Success(Guid.NewGuid()));
        }, CancellationToken.None);
        Assert.False(called);
        Assert.IsType<ValidationError>(result.Error);
    }
}
