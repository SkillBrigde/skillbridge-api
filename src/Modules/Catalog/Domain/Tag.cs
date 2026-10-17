using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Catalog.Domain;

public sealed class Tag : AggregateRoot<Guid>
{
    private Tag() { }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    public static Result<Tag> Create(string? name)
    {
        var slug = CatalogName.CreateSlug(name);
        return slug.IsFailure ? slug.Error : new Tag { Id = Guid.CreateVersion7(), Name = name!.Trim(), Slug = slug.Value };
    }
}
