namespace SkillBridge.Modules.Catalog.Application.DTOs.Responses;

public sealed record CategoryDetailsResponse(Guid Id, string Name, string Slug, string? Description, Guid? ParentId,
    string? IconUrl, int SortOrder, bool IsActive, DateTimeOffset CreatedAtUtc, IReadOnlyList<NamedItemResponse> Skills);
