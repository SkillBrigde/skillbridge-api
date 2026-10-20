using SkillBridge.Modules.Catalog.Domain;

namespace SkillBridge.Modules.Catalog.Application.DTOs.Responses;

public sealed record SkillResponse(Guid Id, Guid CategoryId, string Name, string Slug, string? Description, bool IsActive, DateTimeOffset CreatedAtUtc)
{
    public static SkillResponse From(Skill skill) => new(skill.Id, skill.CategoryId, skill.Name, skill.Slug, skill.Description, skill.IsActive, skill.CreatedAtUtc);
}
