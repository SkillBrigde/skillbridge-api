using Microsoft.EntityFrameworkCore;
using SkillBridge.Modules.Scheduling.Domain;

namespace SkillBridge.Modules.Scheduling.Infrastructure;

public sealed class AvailabilityRuleStore(SchedulingDbContext dbContext, SlotStore slots)
{
    // Phải gọi trong transaction, trước khi đọc/ghi rule. Khóa theo Mentor bảo vệ cả khoảng giờ chưa có bản ghi.
    public async Task<bool> LockSettingsAsync(Guid mentorId, CancellationToken cancellationToken)
    {
        await slots.LockAsync(mentorId, cancellationToken);
        return await dbContext.SchedulingSettings
            .FromSqlInterpolated($"SELECT * FROM scheduling.scheduling_settings WHERE mentor_id = {mentorId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) is not null;
    }

    public Task<bool> HasOverlapAsync(
        Guid mentorId,
        AvailabilityWindow window,
        Guid? excludedRuleId,
        CancellationToken cancellationToken) =>
        dbContext.AvailabilityRules.AnyAsync(rule =>
            rule.MentorId == mentorId && rule.IsActive &&
            rule.DayOfWeek == window.DayOfWeek &&
            rule.StartTime < window.EndTime && rule.EndTime > window.StartTime &&
            (!excludedRuleId.HasValue || rule.Id != excludedRuleId.Value), cancellationToken);
}
