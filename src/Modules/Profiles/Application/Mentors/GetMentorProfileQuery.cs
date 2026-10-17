using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.DTOs.Responses;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record GetMentorProfileQuery(Guid UserId) : IQuery<MentorProfileResponse>;

public sealed class GetMentorProfileQueryHandler(ProfilesDbContext dbContext)
    : IQueryHandler<GetMentorProfileQuery, MentorProfileResponse>
{
    public async Task<Result<MentorProfileResponse>> Handle(GetMentorProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.MentorProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);

        if (profile is null)
        {
            return Error.NotFound("Profiles.MentorProfileNotFound", "Chưa có hồ sơ Mentor cho người dùng hiện tại.");
        }

        return await MentorProfileResponseLoader.LoadAsync(dbContext, profile, cancellationToken);
    }
}
