using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Scheduling.Application;
using SkillBridge.Modules.Scheduling.Domain;

namespace SkillBridge.Modules.Scheduling.Infrastructure;

public sealed class SlotStore(SchedulingDbContext db, TimeProvider clock)
{
    // Cùng khóa cho settings, rules, time-offs và giữ/đặt slot; bảo vệ cả Mentor chưa có settings.
    public Task LockAsync(Guid mentorId, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock(hashtextextended({mentorId.ToString()}, 0))", ct);

    public async Task<Result> RegenerateAsync(Guid mentorId, CancellationToken ct)
    {
        var settings = await db.SchedulingSettings.SingleOrDefaultAsync(x => x.MentorId == mentorId, ct);
        if (settings is null) return Result.Failure(AvailabilityRuleErrors.SettingsRequired);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone)).DateTime);
        var rules = await db.AvailabilityRules.Where(x => x.MentorId == mentorId && x.IsActive).ToListAsync(ct);
        var generated = SlotGenerator.Generate(settings, rules, today).Where(x => x.Start > now).ToHashSet();
        var timeOffs = await db.TimeOffs.Where(x => x.MentorId == mentorId && x.EndDate >= today).ToListAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone);
        bool Blocked(SlotWindow window)
        {
            var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(window.Start, zone).DateTime);
            var end = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(window.End.AddTicks(-1), zone).DateTime);
            return timeOffs.Any(x => x.StartDate <= end && x.EndDate >= start);
        }
        var existing = await db.Slots.Where(x => x.MentorId == mentorId && x.EndAtUtc > now).ToListAsync(ct);
        var protectedSlots = existing.Where(x => x.IsProtected(now)).ToList();
        if (protectedSlots.Any(x => x.StartAtUtc > now && !generated.Contains(new(x.StartAtUtc, x.EndAtUtc)) || Blocked(new(x.StartAtUtc, x.EndAtUtc))))
            return Result.Failure(Error.Conflict("Scheduling.ProtectedSlot", "Thay đổi ảnh hưởng slot đang giữ hoặc đã đặt."));
        var byWindow = existing.ToDictionary(x => new SlotWindow(x.StartAtUtc, x.EndAtUtc));
        foreach (var slot in existing) slot.SetAvailability("Unavailable", now);
        foreach (var window in generated)
        {
            if (!byWindow.TryGetValue(window, out var slot))
            {
                slot = AvailabilitySlot.Create(mentorId, window.Start, window.End, now);
                db.Slots.Add(slot);
            }
            slot.SetAvailability(Blocked(window) ? "Locked" : "Available", now);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
