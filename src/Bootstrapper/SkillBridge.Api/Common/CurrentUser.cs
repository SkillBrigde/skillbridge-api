using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SkillBridge.BuildingBlocks.Security;

namespace SkillBridge.Api.Common;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var rawId = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub");

            return Guid.TryParse(rawId, out var id) ? id : null;
        }
    }

    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue("email");

    public string? Role =>
        User?.FindFirstValue(ClaimTypes.Role) ?? User?.FindFirstValue("role");

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool HasRole(string role) =>
        !string.IsNullOrWhiteSpace(role) && (User?.IsInRole(role) ?? false);
}
