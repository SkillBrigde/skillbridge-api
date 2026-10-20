namespace SkillBridge.Modules.Catalog.Application.DTOs.Requests;

public sealed record UpdateCategoryRequest(string? Name, string? Description = null, Guid? ParentId = null, string? IconUrl = null, int SortOrder = 0, bool IsActive = true);
