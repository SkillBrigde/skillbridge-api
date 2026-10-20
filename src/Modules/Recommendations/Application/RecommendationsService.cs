using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Recommendations.Domain;
using SkillBridge.Modules.Recommendations.Infrastructure;

namespace SkillBridge.Modules.Recommendations.Application;

public sealed class RecommendationsService(RecommendationsDbContext db, IRecommendationSources sources, TimeProvider clock)
{
    public async Task<Result> RefreshMetricsAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync("metrics-refresh", ct); db.ChangeTracker.Clear();
        var input = await sources.LoadMetricsAsync(ct);
        if (input.IsFailure) return Result.Failure(input.Error);
        var existing = await db.Metrics.ToDictionaryAsync(x => x.Id, ct);
        var now = clock.GetUtcNow();
        foreach (var row in input.Value)
        {
            if (!existing.TryGetValue(row.MentorId, out var metric)) { metric = MentorMetric.Create(row.MentorId); db.Metrics.Add(metric); }
            metric.Update(row.UserId, row.SkillIds, row.Prices, row.PaidBookings, row.AcceptedBookings, row.CompletedBookings,
                row.ResponseMinutes, row.RepeatMentees, row.CompletedSessions, row.Rating, row.Reviews, now);
        }
        var present = input.Value.Select(x => x.MentorId).ToHashSet();
        db.Metrics.RemoveRange(existing.Values.Where(x => !present.Contains(x.Id)));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Result.Success();
    }
    public async Task<Result<PagedResult<RecommendedMentor>>> MentorsAsync(Guid user, Guid? skill, int page, int size, CancellationToken ct)
    {
        var offset = Offset(page, size); if (offset.IsFailure) return offset.Error;
        var eligible = await sources.EligibleMentorsAsync(ct); if (eligible.IsFailure) return eligible.Error;
        var query = db.Metrics.AsNoTracking().Where(x => eligible.Value.Contains(x.Id));
        var interests = new Dictionary<Guid, int>();
        var preference = new MenteePreference(null, null, null);
        if (skill.HasValue)
        {
            var skills = await sources.SkillsAsync(ct); if (skills.IsFailure) return skills.Error;
            if (!skills.Value.ContainsKey(skill.Value)) return RecommendationErrors.Missing;
            query = query.Where(x => x.SkillIds.Contains(skill.Value)); interests[skill.Value] = 1;
        }
        else
        {
            var prefs = await sources.PreferenceAsync(user, ct); if (prefs.IsFailure) return prefs.Error;
            preference = prefs.Value;
            var cutoff = clock.GetUtcNow().AddDays(-90);
            var weights = await db.Interactions.AsNoTracking().Where(x => x.UserId == user && x.SkillId != null && x.CreatedAtUtc >= cutoff)
                .GroupBy(x => x.SkillId!.Value).Select(g => new { Id = g.Key, Weight = g.Sum(x => x.InteractionType == "Click" ? 2 : 1) }).ToListAsync(ct);
            interests = weights.ToDictionary(x => x.Id, x => x.Weight);
        }
        var rows = await query.ToListAsync(ct);
        var ranked = rows.Select(x =>
        {
            var value = RecommendationScoring.Calculate(x, interests, preference.BudgetMin, preference.BudgetMax, preference.Currency);
            return new RecommendedMentor(x.Id, value.Score, value.Reasons, new(x.AverageRating, x.TotalSessions, x.AcceptanceRate, x.CompletionRate));
        }).OrderByDescending(x => x.Score).ThenBy(x => x.MentorId).ToArray();
        var items = ranked.Skip(offset.Value).Take(size).ToArray();
        if (user != Guid.Empty)
        {
            db.Logs.Add(new RecommendationLog { UserId = user, MentorIds = items.Select(x => x.MentorId).ToArray(), Scores = items.Select(x => x.Score).ToArray(), CreatedAtUtc = clock.GetUtcNow() });
            await db.SaveChangesAsync(ct);
        }
        return PagedResult<RecommendedMentor>.Create(items, page, size, ranked.Length);
    }
    public async Task<Result> InteractAsync(Guid user, InteractionRequest request, CancellationToken ct)
    {
        if (request.MentorId == Guid.Empty || request.SkillId == Guid.Empty || request.InteractionType is not ("Click" or "ViewProfile"))
            return Result.Failure(Error.Validation("Recommendations.InvalidInteraction", "Cần mentor hợp lệ và Click hoặc ViewProfile."));
        var eligible = await sources.EligibleMentorsAsync(ct); if (eligible.IsFailure) return Result.Failure(eligible.Error);
        if (!eligible.Value.Contains(request.MentorId)) return Result.Failure(RecommendationErrors.Missing);
        if (request.SkillId.HasValue)
        {
            var hasSkill = await sources.HasSkillAsync(request.MentorId, request.SkillId.Value, ct);
            if (hasSkill.IsFailure) return Result.Failure(hasSkill.Error);
            if (!hasSkill.Value) return Result.Failure(Error.Validation("Recommendations.SkillMismatch", "Kỹ năng không hoạt động hoặc không thuộc Mentor."));
        }
        var now = clock.GetUtcNow();
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var identity = $"{user}:{request.MentorId}:{request.SkillId}:{request.InteractionType}:{day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync("interactions:" + user, ct); db.ChangeTracker.Clear();
        if (await db.Interactions.AnyAsync(x => x.DeduplicationKey == key, ct)) return Result.Success();
        if (await db.Interactions.CountAsync(x => x.UserId == user && x.CreatedAtUtc >= day && x.CreatedAtUtc < day.AddDays(1), ct) >= 1000)
            return Result.Failure(Error.Conflict("Recommendations.InteractionLimit", "Đã đạt giới hạn tương tác trong ngày."));
        db.Interactions.Add(new MenteeInteraction
        {
            UserId = user,
            MentorId = request.MentorId,
            SkillId = request.SkillId,
            InteractionType = request.InteractionType,
            DeduplicationKey = key,
            CreatedAtUtc = now
        });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Result.Success();
    }
    public async Task<Result<MentorMetrics>> MetricsAsync(Guid mentor, Guid user, bool admin, CancellationToken ct)
    {
        var owner = await sources.MentorOwnerAsync(mentor, ct); if (owner.IsFailure) return owner.Error;
        if (!admin && owner.Value != user) return Error.Forbidden("Recommendations.Forbidden", "Mentor chỉ xem metrics của chính mình.");
        var row = await db.Metrics.AsNoTracking().SingleOrDefaultAsync(x => x.Id == mentor, ct);
        return row is null ? RecommendationErrors.Unavailable : MentorMetrics.From(row);
    }
    public async Task<Result<IReadOnlyList<TrendingSkill>>> TrendingAsync(CancellationToken ct)
    {
        var skills = await sources.SkillsAsync(ct); if (skills.IsFailure) return skills.Error;
        var cutoff = clock.GetUtcNow().AddDays(-30);
        var counts = await db.Interactions.AsNoTracking().Where(x => x.SkillId != null && x.CreatedAtUtc >= cutoff)
            .GroupBy(x => x.SkillId!.Value).Select(g => new { Skill = g.Key, Score = g.Select(x => x.UserId).Distinct().Count() }).ToListAsync(ct);
        return counts.Where(x => skills.Value.ContainsKey(x.Skill)).OrderByDescending(x => x.Score).ThenBy(x => x.Skill).Take(20)
            .Select(x => new TrendingSkill(x.Skill, skills.Value[x.Skill], x.Score)).ToArray();
    }
    public static Result<int> Offset(int page, int size) => page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue
        ? Error.Validation("Recommendations.InvalidPage", "Phân trang không hợp lệ.") : (page - 1) * size;
    private Task LockAsync(string key, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"recommendations:" + key}, 0))", ct);
}
