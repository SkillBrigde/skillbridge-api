using Microsoft.EntityFrameworkCore;
using SkillBridge.Modules.Profiles.Domain;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.DTOs;

internal static class MentorProfileDtoLoader
{
    public static async Task<MentorProfileDto> LoadAsync(
        ProfilesDbContext dbContext,
        MentorProfile profile,
        CancellationToken cancellationToken)
    {
        var skills = await dbContext.MentorSkills
            .AsNoTracking()
            .Where(skill => skill.MentorProfileId == profile.Id)
            .OrderBy(skill => skill.SkillName)
            .ThenBy(skill => skill.Id)
            .Select(skill => MentorSkillDto.From(skill))
            .ToListAsync(cancellationToken);

        var experiences = await dbContext.MentorExperiences
            .AsNoTracking()
            .Where(experience => experience.MentorProfileId == profile.Id)
            .OrderByDescending(experience => experience.IsCurrent)
            .ThenByDescending(experience => experience.StartDate)
            .ThenBy(experience => experience.Id)
            .Select(experience => MentorExperienceDto.From(experience))
            .ToListAsync(cancellationToken);

        return MentorProfileDto.From(profile, skills, experiences);
    }
}
