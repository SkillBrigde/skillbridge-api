using System.Text.Json.Serialization;

namespace SkillBridge.Modules.Catalog.Application.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MentorServiceRequest(string? Title, string? Description, string? ServiceType,
    int? DurationMinutes, decimal? Price, string? Currency, int? TotalSessions, string? Deliverables, bool? IsActive);
