using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Scheduling.Application.DTOs.Responses;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Modules.Scheduling.Application;

public sealed record GetSchedulingSettingsQuery(Guid UserId) : IQuery<SchedulingSettingsResponse>;

public sealed class GetSchedulingSettingsQueryHandler(
    SchedulingDbContext dbContext,
    IMentorDirectory mentorDirectory) : IQueryHandler<GetSchedulingSettingsQuery, SchedulingSettingsResponse>
{
    public async Task<Result<SchedulingSettingsResponse>> Handle(
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
            : SchedulingSettingsResponse.From(settings);
    }
}
