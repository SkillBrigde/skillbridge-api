namespace SkillBridge.Modules.Catalog.Application.DTOs.Requests;

public sealed record CreateCategoryRequest(string? Name, string? Description = null, Guid? ParentId = null, string? IconUrl = null, int SortOrder = 0);
