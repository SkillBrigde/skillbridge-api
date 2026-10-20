namespace SkillBridge.Modules.Catalog.Application.DTOs.Responses;

public sealed record NamedItemResponse(Guid Id, string Name, string Slug);
