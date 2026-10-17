using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Catalog.Domain;

public sealed class Skill : AggregateRoot<Guid>
{
    private readonly List<Tag> _tags = [];
    private Skill() { }

    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();

    public static Result<Skill> Create(Guid categoryId, string? name, string? description, IReadOnlyCollection<Tag> tags)
    {
        var skill = new Skill { Id = Guid.CreateVersion7(), CreatedAtUtc = DateTimeOffset.UtcNow };
        var result = skill.Update(categoryId, name, description, true, tags);
        return result.IsFailure ? result.Error : skill;
    }

    public Result Update(Guid categoryId, string? name, string? description, bool isActive, IReadOnlyCollection<Tag> tags)
    {
        var slug = CatalogName.CreateSlug(name);
        if (slug.IsFailure) return Result.Failure(slug.Error);
        if (categoryId == Guid.Empty)
            return Result.Failure(Error.Validation("Catalog.InvalidCategory", "Category ID không hợp lệ."));
        if (!CatalogName.IsValidDescription(description))
            return Result.Failure(Error.Validation("Catalog.InvalidDescription", "Mô tả tối đa 4000 ký tự và không chứa ký tự null."));

        CategoryId = categoryId;
        Name = name!.Trim();
        Slug = slug.Value;
        Description = description?.Trim();
        IsActive = isActive;
        var replacement = tags.DistinctBy(tag => tag.Id).ToArray();
        _tags.Clear();
        _tags.AddRange(replacement);
        return Result.Success();
    }
}
