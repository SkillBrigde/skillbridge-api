using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Catalog.Domain;

namespace SkillBridge.Modules.Catalog.Application;

public sealed record SkillCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredOnUtc, Guid SkillId,
    Guid CategoryId, string Name, string Slug) : IIntegrationEvent;

public static class CatalogPagination
{
    public static Result<int> Offset(int page, int pageSize) =>
        page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue
            ? Error.Validation("Catalog.InvalidPagination", "page phải từ 1, pageSize từ 1 đến 100; vị trí trang không được vượt quá Int32.MaxValue.")
            : Result.Success((page - 1) * pageSize);
}
