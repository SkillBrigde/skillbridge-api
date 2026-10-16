using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public interface ISkillDirectory
{
    Task<Result<SkillDirectoryItem>> GetActiveAsync(Guid skillId, CancellationToken cancellationToken);
}

public sealed record SkillDirectoryItem(Guid Id, string Name);

public static class SkillDirectoryErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Profiles.SkillNotFound",
        "Không tìm thấy kỹ năng đang hoạt động.");

    public static readonly Error Unavailable = Error.Failure(
        "Profiles.SkillDirectoryUnavailable",
        "Danh mục kỹ năng tạm thời chưa khả dụng.");
}

internal sealed class UnavailableSkillDirectory : ISkillDirectory
{
    public Task<Result<SkillDirectoryItem>> GetActiveAsync(
        Guid skillId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<SkillDirectoryItem>(SkillDirectoryErrors.Unavailable));
}
