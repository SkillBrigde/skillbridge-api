namespace SkillBridge.Modules.Catalog.Application.DTOs.Requests;

public sealed record UpdateSkillRequest(Guid CategoryId, string? Name, string? Description = null, bool IsActive = true, Guid[]? TagIds = null);
