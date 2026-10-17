namespace SkillBridge.Modules.Catalog.Application.DTOs.Requests;

public sealed record CreateSkillRequest(Guid CategoryId, string? Name, string? Description = null, Guid[]? TagIds = null);
