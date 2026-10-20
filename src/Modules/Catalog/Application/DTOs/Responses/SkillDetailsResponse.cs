namespace SkillBridge.Modules.Catalog.Application.DTOs.Responses;

public sealed record SkillDetailsResponse(Guid Id, Guid CategoryId, string Name, string Slug, string? Description,
    bool IsActive, DateTimeOffset CreatedAtUtc, IReadOnlyList<NamedItemResponse> Tags);
