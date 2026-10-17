using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.DTOs;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record GetPublicMentorProfileQuery(Guid MentorId) : IQuery<MentorProfileDto>;

public sealed class GetPublicMentorProfileQueryHandler(ProfilesDbContext dbContext)
    : IQueryHandler<GetPublicMentorProfileQuery, MentorProfileDto>
{
    public async Task<Result<MentorProfileDto>> Handle(
        GetPublicMentorProfileQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.MentorProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == request.MentorId && item.VerificationStatus != "Rejected",
                cancellationToken);

        if (profile is null)
        {
            return Error.NotFound("Profiles.MentorProfileNotFound", "Không tìm thấy hồ sơ Mentor.");
        }

        return await MentorProfileDtoLoader.LoadAsync(dbContext, profile, cancellationToken);
    }
}
