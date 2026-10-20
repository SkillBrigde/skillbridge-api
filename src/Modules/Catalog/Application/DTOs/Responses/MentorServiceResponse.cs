using SkillBridge.Modules.Catalog.Domain;

namespace SkillBridge.Modules.Catalog.Application.DTOs.Responses;

public sealed record MentorServiceResponse(Guid Id, Guid MentorId, string Title, string Description, string ServiceType,
    int DurationMinutes, decimal Price, string Currency, int TotalSessions, string Deliverables,
    bool IsActive, long Version, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc)
{
    public static MentorServiceResponse From(MentorService service) => new(service.Id, service.MentorId,
        service.Title, service.Description, service.ServiceType, service.DurationMinutes, service.Price,
        service.Currency, service.TotalSessions, service.Deliverables, service.IsActive, service.Version,
        service.CreatedAtUtc, service.UpdatedAtUtc);
}
