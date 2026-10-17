using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Scheduling.Domain;

namespace SkillBridge.Modules.Scheduling.Application;

public interface IMentorDirectory
{
    Task<Result<Guid>> GetMentorIdAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record SchedulingSettingsDto(
    Guid Id,
    Guid MentorId,
    int SessionDurationMinutes,
    int BufferTimeMinutes,
    int LeadTimeHours,
    int MaxDaysInAdvance,
    string Timezone)
{
    internal static SchedulingSettingsDto From(SchedulingSettings settings) => new(
        settings.Id,
        settings.MentorId,
        settings.SessionDurationMinutes,
        settings.BufferTimeMinutes,
        settings.LeadTimeHours,
        settings.MaxDaysInAdvance,
        settings.Timezone);
}

public static class SchedulingSettingsErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Scheduling.SettingsNotFound",
        "Mentor chưa cấu hình cài đặt lịch.");

    public static readonly Error ProfileRequired = Error.Conflict(
        "Scheduling.MentorProfileRequired",
        "Hãy tạo hồ sơ Mentor trước khi cấu hình lịch.");

    public static readonly Error MentorDirectoryUnavailable = Error.Failure(
        "Scheduling.MentorDirectoryUnavailable",
        "Nguồn hồ sơ Mentor tạm thời chưa khả dụng.");
}

internal sealed class UnavailableMentorDirectory : IMentorDirectory
{
    public Task<Result<Guid>> GetMentorIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<Guid>(SchedulingSettingsErrors.MentorDirectoryUnavailable));
}
