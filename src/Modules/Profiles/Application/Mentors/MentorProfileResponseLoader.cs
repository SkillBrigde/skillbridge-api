using Microsoft.EntityFrameworkCore;
using SkillBridge.Modules.Profiles.Application.DTOs.Responses;
using SkillBridge.Modules.Profiles.Domain;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

internal static class MentorProfileResponseLoader
{
    public static async Task<MentorProfileResponse> LoadAsync(
        ProfilesDbContext dbContext,
        MentorProfile profile,
        CancellationToken cancellationToken)
    {
        var skills = await dbContext.MentorSkills
            .AsNoTracking()
            .Where(skill => skill.MentorProfileId == profile.Id)
            .OrderBy(skill => skill.SkillName)
            .ThenBy(skill => skill.Id)
            .Select(skill => MentorSkillResponse.From(skill))
            .ToListAsync(cancellationToken);

        var experiences = await dbContext.MentorExperiences
            .AsNoTracking()
            .Where(experience => experience.MentorProfileId == profile.Id)
            .OrderByDescending(experience => experience.IsCurrent)
            .ThenByDescending(experience => experience.StartDate)
            .ThenBy(experience => experience.Id)
            .Select(experience => MentorExperienceResponse.From(experience))
            .ToListAsync(cancellationToken);

        var certifications = await dbContext.MentorCertifications
            .AsNoTracking()
            .Where(certification => certification.MentorProfileId == profile.Id)
            .OrderByDescending(certification => certification.IssueDate)
            .ThenBy(certification => certification.Id)
            .Select(certification => MentorCertificationResponse.From(certification))
            .ToListAsync(cancellationToken);

        return MentorProfileResponse.From(profile, skills, experiences, certifications);
    }
}
