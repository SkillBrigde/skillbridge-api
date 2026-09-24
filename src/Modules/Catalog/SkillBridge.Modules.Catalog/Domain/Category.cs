using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Catalog.Domain;

public sealed class Category : AggregateRoot<Guid>
{
    private Category() { }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? ParentId { get; private set; }
    public string? IconUrl { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Result<Category> Create(string? name, string? description, Guid? parentId, string? iconUrl, int sortOrder)
    {
        var category = new Category { Id = Guid.CreateVersion7(), CreatedAtUtc = DateTimeOffset.UtcNow };
        var result = category.Update(name, description, parentId, iconUrl, sortOrder, true);
        return result.IsFailure ? result.Error : category;
    }

    public Result Update(string? name, string? description, Guid? parentId, string? iconUrl, int sortOrder, bool isActive)
    {
        var slug = CatalogName.CreateSlug(name);
        if (slug.IsFailure) return Result.Failure(slug.Error);
        if (!CatalogName.IsValidDescription(description))
            return Result.Failure(Error.Validation("Catalog.InvalidDescription", "Mô tả tối đa 4000 ký tự và không chứa ký tự null."));
        if (parentId == Guid.Empty || parentId == Id)
            return Result.Failure(Error.Validation("Catalog.InvalidParent", "Danh mục cha không hợp lệ."));
        if (sortOrder < 0)
            return Result.Failure(Error.Validation("Catalog.InvalidSortOrder", "Thứ tự phải lớn hơn hoặc bằng 0."));
        if (!string.IsNullOrWhiteSpace(iconUrl) && (iconUrl.Length > 2048 || iconUrl.Any(char.IsControl) ||
            !Uri.TryCreate(iconUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            return Result.Failure(Error.Validation("Catalog.InvalidIconUrl", "Icon URL phải là URL HTTP/HTTPS hợp lệ, tối đa 2048 ký tự."));

        Name = name!.Trim();
        Slug = slug.Value;
        Description = description?.Trim();
        ParentId = parentId;
        IconUrl = string.IsNullOrWhiteSpace(iconUrl) ? null : iconUrl.Trim();
        SortOrder = sortOrder;
        IsActive = isActive;
        return Result.Success();
    }

    public void Delete()
    {
        IsDeleted = true;
        IsActive = false;
        DeletedAtUtc = DateTimeOffset.UtcNow;
    }
}
