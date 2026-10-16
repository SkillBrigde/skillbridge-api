using MediatR;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Catalog.Application;
using SkillBridge.Modules.Profiles.Application.Mentors;

namespace SkillBridge.Api.Common;

public static class CatalogComposition
{
    public static IServiceCollection AddCatalogComposition(this IServiceCollection services) =>
        services.AddScoped<IMentorDirectory, ProfileMentorDirectory>();
}

internal sealed class ProfileMentorDirectory(ISender sender) : IMentorDirectory
{
    public async Task<Result<Guid>> GetMentorIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMentorProfileQuery(userId), cancellationToken);
        if (result.IsSuccess) return result.Value.Id;
        return result.Error.Type == ErrorType.NotFound ? MentorServiceErrors.ProfileRequired : MentorServiceErrors.Unavailable;
    }
}
