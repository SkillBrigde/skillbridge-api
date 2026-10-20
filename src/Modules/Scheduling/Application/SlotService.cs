using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Scheduling.Application.DTOs.Requests;
using SkillBridge.Modules.Scheduling.Application.DTOs.Responses;
using SkillBridge.Modules.Scheduling.Domain;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Modules.Scheduling.Application;

public sealed class SlotService(SchedulingDbContext db, SlotStore store, IMentorDirectory mentors,
    IValidator<TimeOffRequest> validator, TimeProvider clock)
{
    public async Task<Result<PagedResult<SlotResponse>>> ListSlotsAsync(Guid mentorId, DateOnly start, DateOnly end,
        int page, int pageSize, CancellationToken ct)
    {
        var offset = Offset(page, pageSize);
        if (offset.IsFailure) return offset.Error;
        if (mentorId == Guid.Empty || start > end || end.DayNumber - start.DayNumber > 365 || start.Year < 2000 || end.Year > 9998)
            return Error.Validation("Scheduling.InvalidDateRange", "Khoảng ngày xem slot không hợp lệ hoặc vượt 366 ngày.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await store.LockAsync(mentorId, ct);
        var settings = await db.SchedulingSettings.SingleOrDefaultAsync(x => x.MentorId == mentorId, ct);
        if (settings is null) return SchedulingSettingsErrors.NotFound;
        var generated = await store.RegenerateAsync(mentorId, ct);
        if (generated.IsFailure) return generated.Error;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone);
        var now = clock.GetUtcNow();
        var earliest = now.AddHours(settings.LeadTimeHours);
        // Giới hạn ngày ở query sau khi convert timezone; không lộ booking hoặc lý do nghỉ.
        var candidates = await db.Slots.AsNoTracking().Where(x => x.MentorId == mentorId && x.Status == "Available" && x.StartAtUtc > now && x.StartAtUtc >= earliest)
            .OrderBy(x => x.StartAtUtc).ThenBy(x => x.Id).ToListAsync(ct);
        var filtered = candidates.Where(x =>
        {
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.StartAtUtc, zone).DateTime);
            return date >= start && date <= end;
        }).ToList();
        await transaction.CommitAsync(ct);
        return PagedResult<SlotResponse>.Create(filtered.Skip(offset.Value).Take(pageSize).Select(SlotResponse.From).ToList(), page, pageSize, filtered.Count);
    }

    public async Task<Result<TimeOffResponse>> AddTimeOffAsync(Guid userId, TimeOffRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return new ValidationError(validation.Errors.GroupBy(x => x.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).Distinct().ToArray()));
        var mentor = await mentors.GetMentorIdAsync(userId, ct);
        if (mentor.IsFailure) return mentor.Error;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await store.LockAsync(mentor.Value, ct);
        var settings = await db.SchedulingSettings.SingleOrDefaultAsync(x => x.MentorId == mentor.Value, ct);
        if (settings is null) return AvailabilityRuleErrors.SettingsRequired;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone)).DateTime);
        if (request.StartDate < today || request.EndDate > today.AddDays(365))
            return Error.Validation("Scheduling.InvalidTimeOffDate", "Ngày nghỉ phải từ hôm nay đến tối đa 365 ngày tới theo múi giờ Mentor.");
        if (await db.TimeOffs.AnyAsync(x => x.MentorId == mentor.Value && x.StartDate <= request.EndDate && x.EndDate >= request.StartDate, ct))
            return Error.Conflict("Scheduling.TimeOffOverlap", "Khoảng nghỉ chồng với ngày nghỉ đã có.");
        var created = TimeOff.Create(mentor.Value, request.StartDate!.Value, request.EndDate!.Value, request.Reason, clock.GetUtcNow());
        if (created.IsFailure) return created.Error;
        db.TimeOffs.Add(created.Value);
        await db.SaveChangesAsync(ct);
        var regenerated = await store.RegenerateAsync(mentor.Value, ct);
        if (regenerated.IsFailure) return regenerated.Error;
        await transaction.CommitAsync(ct);
        return TimeOffResponse.From(created.Value);
    }

    public async Task<Result> RemoveTimeOffAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var mentor = await mentors.GetMentorIdAsync(userId, ct);
        if (mentor.IsFailure) return Result.Failure(mentor.Error);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await store.LockAsync(mentor.Value, ct);
        var item = await db.TimeOffs.SingleOrDefaultAsync(x => x.Id == id && x.MentorId == mentor.Value, ct);
        if (item is null) return Result.Failure(Error.NotFound("Scheduling.TimeOffNotFound", "Không tìm thấy ngày nghỉ."));
        db.TimeOffs.Remove(item);
        await db.SaveChangesAsync(ct);
        var generated = await store.RegenerateAsync(mentor.Value, ct);
        if (generated.IsFailure) return generated;
        await transaction.CommitAsync(ct);
        return Result.Success();
    }

    public async Task<Result<PagedResult<TimeOffResponse>>> ListTimeOffsAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var offset = Offset(page, pageSize);
        if (offset.IsFailure) return offset.Error;
        var mentor = await mentors.GetMentorIdAsync(userId, ct);
        if (mentor.IsFailure) return mentor.Error;
        var query = db.TimeOffs.AsNoTracking().Where(x => x.MentorId == mentor.Value);
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.StartDate).ThenBy(x => x.Id).Skip(offset.Value).Take(pageSize).ToListAsync(ct);
        return PagedResult<TimeOffResponse>.Create(items.Select(TimeOffResponse.From).ToList(), page, pageSize, count);
    }

    private static Result<int> Offset(int page, int pageSize) => page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue
        ? Error.Validation("Scheduling.InvalidPagination", "page phải từ 1, pageSize 1–100 và offset không vượt Int32.MaxValue.")
        : (page - 1) * pageSize;
}
