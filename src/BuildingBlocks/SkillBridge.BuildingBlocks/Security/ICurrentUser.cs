namespace SkillBridge.BuildingBlocks.Security;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    bool HasRole(string role);
}
