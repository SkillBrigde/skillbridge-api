using System.Security.Claims;

namespace SkillBridge.BuildingBlocks.Security;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(Guid userId, string email, string fullName, IEnumerable<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
