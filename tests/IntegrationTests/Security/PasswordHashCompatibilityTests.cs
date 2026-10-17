using SkillBridge.BuildingBlocks.Security;

namespace SkillBridge.IntegrationTests.Security;

public sealed class PasswordHashCompatibilityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("!RESET_REQUIRED!")]
    [InlineData("not-a-bcrypt-hash")]
    [InlineData("$2b$11$")]
    [InlineData("$2b$xx$abcdefghijklmnopqrstuu")]
    [InlineData("$2b$99$abcdefghijklmnopqrstuu")]
    public void Missing_malformed_or_recovery_only_hashes_are_rejected_without_throwing(string? hash)
    {
        var hasher = new PasswordHasher();

        Assert.False(hasher.VerifyPassword("FixturePassword1!", hash!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void Empty_passwords_cannot_authenticate_a_recovery_only_account(string? password)
    {
        var hasher = new PasswordHasher();

        Assert.False(hasher.VerifyPassword(password!, "!RESET_REQUIRED!"));
    }

    [Fact]
    public void Existing_valid_BCrypt_hashes_still_verify_only_the_correct_password()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.HashPassword("FixturePassword1!");

        Assert.True(hasher.VerifyPassword("FixturePassword1!", hash));
        Assert.False(hasher.VerifyPassword("WrongFixturePassword1!", hash));
    }
}
