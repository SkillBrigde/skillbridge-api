using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.DTOs.Responses;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record GetPublicMentorProfileQuery(Guid MentorId) : IQuery<MentorProfileResponse>;

public sealed class GetPublicMentorProfileQueryHandler(ProfilesDbContext dbContext)
    : IQueryHandler<GetPublicMentorProfileQuery, MentorProfileResponse>
{
    public async Task<Result<MentorProfileResponse>> Handle(
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

        return await MentorProfileResponseLoader.LoadAsync(dbContext, profile, cancellationToken);
    }
}
