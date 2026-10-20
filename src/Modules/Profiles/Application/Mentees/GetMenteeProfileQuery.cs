using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.DTOs.Responses;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentees;

public sealed record GetMenteeProfileQuery(Guid UserId) : IQuery<MenteeProfileResponse>;

public sealed class GetMenteeProfileQueryHandler(ProfilesDbContext dbContext)
    : IQueryHandler<GetMenteeProfileQuery, MenteeProfileResponse>
{
    public async Task<Result<MenteeProfileResponse>> Handle(GetMenteeProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.MenteeProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);

        if (profile is null)
        {
            return Error.NotFound("Profiles.MenteeProfileNotFound", "Chưa có hồ sơ Mentee cho người dùng hiện tại.");
        }

        return new MenteeProfileResponse(
            profile.Id,
            profile.CareerGoal,
            profile.CurrentLevel,
            profile.Timezone,
            profile.BudgetMin,
            profile.BudgetMax,
            profile.Currency,
            profile.AvatarUrl,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc);
    }
}
