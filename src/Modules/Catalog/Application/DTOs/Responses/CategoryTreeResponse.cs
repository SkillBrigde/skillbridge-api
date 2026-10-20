using SkillBridge.Modules.Catalog.Domain;

namespace SkillBridge.Modules.Catalog.Application.DTOs.Responses;

public sealed record CategoryTreeResponse(Guid Id, string Name, string Slug, string? Description, Guid? ParentId,
    string? IconUrl, int SortOrder, bool IsActive, DateTimeOffset CreatedAtUtc, List<CategoryTreeResponse> Children)
{
    public static CategoryTreeResponse From(Category category) => new(category.Id, category.Name, category.Slug,
        category.Description, category.ParentId, category.IconUrl, category.SortOrder, category.IsActive, category.CreatedAtUtc, []);
}
