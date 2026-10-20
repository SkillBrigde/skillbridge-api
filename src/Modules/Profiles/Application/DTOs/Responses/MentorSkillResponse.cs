using SkillBridge.Modules.Profiles.Domain;

namespace SkillBridge.Modules.Profiles.Application.DTOs.Responses;

public sealed record MentorSkillResponse(
    Guid Id,
    Guid SkillId,
    string Name,
    string ProficiencyLevel,
    int YearsOfExperience)
{
    internal static MentorSkillResponse From(MentorSkill skill) => new(
        skill.Id,
        skill.SkillId,
        skill.SkillName,
        skill.ProficiencyLevel,
        skill.YearsOfExperience);
}
