using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.BuildingBlocks.Security;

namespace SkillBridge.Tests.Security;

public sealed class JwtConfigurationTests
{
    [Fact]
    public void JWT_settings_do_not_supply_a_default_signing_key()
    {
        Assert.Empty(new JwtSettings().SecretKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    [InlineData("fixture-short-key")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    [InlineData("ééééééééééééééé")]
    public void Invalid_signing_keys_fail_during_registration_without_disclosing_the_key(string? key)
    {
        var services = new ServiceCollection();
        var configuration = Configuration(key);

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSecurityServices(configuration));

        Assert.Contains("Jwt__SecretKey", exception.Message);
        Assert.Contains("32", exception.Message);
        Assert.Contains("UTF-8", exception.Message);
        if (!string.IsNullOrWhiteSpace(key))
            Assert.DoesNotContain(key, exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(32, 'x')]
    [InlineData(16, 'é')]
    [InlineData(11, '界')]
    public void Signing_keys_with_at_least_32_UTF8_bytes_are_accepted(int characters, char character)
    {
        var key = new string(character, characters);
        var services = new ServiceCollection();

        services.AddSecurityServices(Configuration(key));
        using var provider = services.BuildServiceProvider();

        Assert.Equal(key, provider.GetRequiredService<JwtSettings>().SecretKey);
        Assert.NotNull(provider.GetRequiredService<IJwtTokenGenerator>());
    }

    private static IConfiguration Configuration(string? key)
    {
        var values = new Dictionary<string, string?>();
        if (key != null)
            values["Jwt:SecretKey"] = key;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
