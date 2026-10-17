using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Catalog.Application;
using SkillBridge.Modules.Profiles.Application.Mentors;

namespace SkillBridge.Api.Common;

public static class ProfilesComposition
{
    public static IServiceCollection AddProfilesComposition(this IServiceCollection services) =>
        services.AddScoped<ISkillDirectory, CatalogSkillDirectory>();
}

internal sealed class CatalogSkillDirectory(CatalogService catalog) : ISkillDirectory
{
    public async Task<Result<SkillDirectoryItem>> GetActiveAsync(
        Guid skillId,
        CancellationToken cancellationToken)
    {
        var result = await catalog.GetSkillAsync(skillId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.NotFound
                ? SkillDirectoryErrors.NotFound
                : SkillDirectoryErrors.Unavailable;
        }

        if (!result.Value.IsActive)
        {
            return SkillDirectoryErrors.NotFound;
        }

        return new SkillDirectoryItem(result.Value.Id, result.Value.Name);
    }
}
