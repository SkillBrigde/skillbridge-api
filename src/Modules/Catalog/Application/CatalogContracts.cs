using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Catalog.Domain;

namespace SkillBridge.Modules.Catalog.Application;

public sealed record CreateCategoryRequest(string? Name, string? Description = null, Guid? ParentId = null, string? IconUrl = null, int SortOrder = 0);
public sealed record UpdateCategoryRequest(string? Name, string? Description = null, Guid? ParentId = null, string? IconUrl = null, int SortOrder = 0, bool IsActive = true);
public sealed record CreateSkillRequest(Guid CategoryId, string? Name, string? Description = null, Guid[]? TagIds = null);
public sealed record UpdateSkillRequest(Guid CategoryId, string? Name, string? Description = null, bool IsActive = true, Guid[]? TagIds = null);
public sealed record CreateTagRequest(string? Name);

public sealed record CategoryDto(Guid Id, string Name, string Slug, string? Description, Guid? ParentId,
    string? IconUrl, int SortOrder, bool IsActive, DateTimeOffset CreatedAtUtc)
{
    public static CategoryDto From(Category category) => new(category.Id, category.Name, category.Slug,
        category.Description, category.ParentId, category.IconUrl, category.SortOrder, category.IsActive, category.CreatedAtUtc);
}

public sealed record CategoryTreeDto(Guid Id, string Name, string Slug, string? Description, Guid? ParentId,
    string? IconUrl, int SortOrder, bool IsActive, DateTimeOffset CreatedAtUtc, List<CategoryTreeDto> Children)
{
    public static CategoryTreeDto From(Category category) => new(category.Id, category.Name, category.Slug,
        category.Description, category.ParentId, category.IconUrl, category.SortOrder, category.IsActive, category.CreatedAtUtc, []);
}

public sealed record CategoryDetailsDto(Guid Id, string Name, string Slug, string? Description, Guid? ParentId,
    string? IconUrl, int SortOrder, bool IsActive, DateTimeOffset CreatedAtUtc, IReadOnlyList<NamedItemDto> Skills);
public sealed record NamedItemDto(Guid Id, string Name, string Slug);
public sealed record SkillDto(Guid Id, Guid CategoryId, string Name, string Slug, string? Description, bool IsActive, DateTimeOffset CreatedAtUtc)
{
    public static SkillDto From(Skill skill) => new(skill.Id, skill.CategoryId, skill.Name, skill.Slug, skill.Description, skill.IsActive, skill.CreatedAtUtc);
}
public sealed record SkillDetailsDto(Guid Id, Guid CategoryId, string Name, string Slug, string? Description,
    bool IsActive, DateTimeOffset CreatedAtUtc, IReadOnlyList<NamedItemDto> Tags);

public sealed record SkillCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredOnUtc, Guid SkillId,
    Guid CategoryId, string Name, string Slug) : IIntegrationEvent;

public static class CatalogPagination
{
    public static Result<int> Offset(int page, int pageSize) =>
        page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue
            ? Error.Validation("Catalog.InvalidPagination", "page phải từ 1, pageSize từ 1 đến 100; vị trí trang không được vượt quá Int32.MaxValue.")
            : Result.Success((page - 1) * pageSize);
}
