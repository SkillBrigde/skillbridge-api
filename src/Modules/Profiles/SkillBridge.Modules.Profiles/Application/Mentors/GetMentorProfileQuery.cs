using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.DTOs;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record GetMentorProfileQuery(Guid UserId) : IQuery<MentorProfileDto>;

public sealed class GetMentorProfileQueryHandler(ProfilesDbContext dbContext)
    : IQueryHandler<GetMentorProfileQuery, MentorProfileDto>
{
    public async Task<Result<MentorProfileDto>> Handle(GetMentorProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.MentorProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);

        if (profile is null)
        {
            return Error.NotFound("Profiles.MentorProfileNotFound", "Chưa có hồ sơ Mentor cho người dùng hiện tại.");
        }

        return await MentorProfileDtoLoader.LoadAsync(dbContext, profile, cancellationToken);
    }
}
