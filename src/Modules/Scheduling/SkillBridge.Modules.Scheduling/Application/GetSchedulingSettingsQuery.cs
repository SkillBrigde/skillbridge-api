using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Modules.Scheduling.Application;

public sealed record GetSchedulingSettingsQuery(Guid UserId) : IQuery<SchedulingSettingsDto>;

public sealed class GetSchedulingSettingsQueryHandler(
    SchedulingDbContext dbContext,
    IMentorDirectory mentorDirectory) : IQueryHandler<GetSchedulingSettingsQuery, SchedulingSettingsDto>
{
    public async Task<Result<SchedulingSettingsDto>> Handle(
        GetSchedulingSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var mentorId = await mentorDirectory.GetMentorIdAsync(request.UserId, cancellationToken);
        if (mentorId.IsFailure)
        {
            return mentorId.Error;
        }

        var settings = await dbContext.SchedulingSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.MentorId == mentorId.Value, cancellationToken);

        return settings is null
            ? SchedulingSettingsErrors.NotFound
            : SchedulingSettingsDto.From(settings);
    }
}
