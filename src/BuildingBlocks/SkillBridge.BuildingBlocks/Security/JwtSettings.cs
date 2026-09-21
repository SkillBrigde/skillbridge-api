namespace SkillBridge.BuildingBlocks.Security;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = "SkillBridgeSuperSecretKey2026WithSufficientLengthForHmacSha256Security!";
    public string Issuer { get; set; } = "SkillBridge.Api";
    public string Audience { get; set; } = "SkillBridge.Client";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
