using SkillBridge.BuildingBlocks.Domain;

namespace SkillBridge.Modules.Identity.Domain;

public sealed class Role : Entity<Guid>
{
    public string Name { get; private set; } = default!;
    public string NormalizedName { get; private set; } = default!;
    public string? Description { get; private set; }

    private Role() { }

    public static Role Create(string name, string? description = null)
    {
        return new Role
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            NormalizedName = name.Trim().ToUpperInvariant(),
            Description = description
        };
    }
}
