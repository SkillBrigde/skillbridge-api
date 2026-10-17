using SkillBridge.Modules.Profiles.Domain;

namespace SkillBridge.Modules.Profiles.Application.DTOs.Responses;

public sealed record MentorExperienceResponse(
    Guid Id,
    string Company,
    string Position,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    string? Description,
    bool IsCurrent)
{
    internal static MentorExperienceResponse From(MentorExperience experience) => new(
        experience.Id,
        experience.CompanyName,
        experience.Position,
        experience.StartDate,
        experience.EndDate,
        experience.Description,
        experience.IsCurrent);
}
