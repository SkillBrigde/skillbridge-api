namespace SkillBridge.Modules.Identity.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "SkillBridge";
    public string Audience { get; set; } = "SkillBridge.Bff";
    public string SigningKey { get; set; } = string.Empty;
}
