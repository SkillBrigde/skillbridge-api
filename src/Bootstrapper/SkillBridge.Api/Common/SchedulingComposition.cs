using MediatR;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.Mentors;
using SkillBridge.Modules.Scheduling.Application;

namespace SkillBridge.Api.Common;

public static class SchedulingComposition
{
    public static IServiceCollection AddSchedulingComposition(this IServiceCollection services) =>
        services.AddScoped<IMentorDirectory, ProfileSchedulingMentorDirectory>();
}

internal sealed class ProfileSchedulingMentorDirectory(ISender sender) : IMentorDirectory
{
    public async Task<Result<Guid>> GetMentorIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMentorProfileQuery(userId), cancellationToken);
        if (result.IsSuccess)
        {
            return result.Value.Id;
        }

        return result.Error.Type == ErrorType.NotFound
            ? SchedulingSettingsErrors.ProfileRequired
            : SchedulingSettingsErrors.MentorDirectoryUnavailable;
    }
}
