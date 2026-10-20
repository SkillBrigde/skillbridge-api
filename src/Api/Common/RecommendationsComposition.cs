using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Infrastructure;
using SkillBridge.Modules.Catalog.Infrastructure;
using SkillBridge.Modules.Identity.Infrastructure.Persistence;
using SkillBridge.Modules.Learning.Infrastructure;
using SkillBridge.Modules.Profiles.Infrastructure;
using SkillBridge.Modules.Recommendations.Application;
using SkillBridge.Modules.Recommendations.Domain;
using SkillBridge.Modules.Reviews.Infrastructure;

namespace SkillBridge.Api.Common;

public static class RecommendationsComposition
{
    public static IServiceCollection AddRecommendationsComposition(this IServiceCollection services) => services.AddScoped<IRecommendationSources, RecommendationSources>();
}
internal sealed class RecommendationSources(ProfilesDbContext profiles, CatalogDbContext catalog, IdentityDbContext identity,
    BookingDbContext bookings, LearningDbContext learning, ReviewsDbContext reviews, TimeProvider clock) : IRecommendationSources
{
    public async Task<Result<IReadOnlyList<Guid>>> EligibleMentorsAsync(CancellationToken ct)
    {
        var users = await identity.Users.AsNoTracking().Where(x => x.IsActive && x.Role == "Mentor").Select(x => x.Id).ToListAsync(ct);
        var mentors = await profiles.MentorProfiles.AsNoTracking().Where(x => users.Contains(x.UserId) && x.IsAcceptingMentees && x.VerificationStatus != "Rejected")
            .Select(x => new { x.Id, x.UserId }).ToListAsync(ct);
        var ids = mentors.Select(x => x.Id).ToArray();
        var services = await catalog.MentorServices.AsNoTracking().Where(x => x.IsActive && ids.Contains(x.MentorId)).Select(x => new { x.MentorId, x.MentorUserId }).ToListAsync(ct);
        return mentors.Where(x => services.Any(s => s.MentorId == x.Id && s.MentorUserId == x.UserId)).Select(x => x.Id).ToArray();
    }
    public async Task<Result<IReadOnlyDictionary<Guid, string>>> SkillsAsync(CancellationToken ct)
    {
        var categories = await catalog.Categories.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted).Select(x => x.Id).ToListAsync(ct);
        return await catalog.Skills.AsNoTracking().Where(x => x.IsActive && categories.Contains(x.CategoryId)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
    }
    public async Task<Result<MenteePreference>> PreferenceAsync(Guid user, CancellationToken ct)
    {
        var profile = await profiles.MenteeProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user, ct);
        return profile is null ? new MenteePreference(null, null, null) : new MenteePreference(profile.BudgetMin, profile.BudgetMax, profile.Currency);
    }
    public async Task<Result<Guid>> MentorOwnerAsync(Guid mentor, CancellationToken ct)
    {
        var owner = await profiles.MentorProfiles.AsNoTracking().Where(x => x.Id == mentor).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct);
        return owner.HasValue ? owner.Value : RecommendationErrors.Missing;
    }
    public async Task<Result<bool>> HasSkillAsync(Guid mentor, Guid skill, CancellationToken ct)
    {
        var skills = await SkillsAsync(ct); if (skills.IsFailure) return skills.Error;
        return skills.Value.ContainsKey(skill) && await profiles.MentorSkills.AsNoTracking().AnyAsync(x => x.MentorProfileId == mentor && x.SkillId == skill, ct);
    }
    public async Task<Result<IReadOnlyList<MetricInput>>> LoadMetricsAsync(CancellationToken ct)
    {
        var mentors = await profiles.MentorProfiles.AsNoTracking().ToListAsync(ct);
        var ids = mentors.Select(x => x.Id).ToArray();
        var skills = await SkillsAsync(ct); if (skills.IsFailure) return skills.Error;
        var mentorSkills = await profiles.MentorSkills.AsNoTracking().Where(x => ids.Contains(x.MentorProfileId)).ToListAsync(ct);
        var services = await catalog.MentorServices.AsNoTracking().Where(x => x.IsActive && ids.Contains(x.MentorId)).ToListAsync(ct);
        var orders = await bookings.Bookings.AsNoTracking().Include(x => x.AuditLogs).Where(x => x.PaymentId != null).AsSplitQuery().ToListAsync(ct);
        var now = clock.GetUtcNow();
        var sessions = await learning.Sessions.AsNoTracking().Where(x => x.Status == "Completed" && x.ActualEndUtc != null && x.ActualEndUtc <= now)
            .GroupBy(x => x.MentorId).Select(g => new { Mentor = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Mentor, x => x.Count, ct);
        var ratings = await reviews.Reviews.AsNoTracking().Where(x => x.IsVisible).GroupBy(x => x.MentorId)
            .Select(g => new { Mentor = g.Key, Average = g.Average(x => x.OverallRating), Count = g.Count() }).ToDictionaryAsync(x => x.Mentor, ct);
        var output = new List<MetricInput>();
        foreach (var mentor in mentors)
        {
            var history = orders.Where(x => x.Terms.MentorId == mentor.Id && x.MentorUserId == mentor.UserId).ToArray();
            var accepted = history.Where(x => x.AuditLogs.Any(a => a.NewStatus == "Confirmed")).ToArray();
            var responseTimes = accepted.Select(x =>
            {
                var paid = x.AuditLogs.Where(a => a.NewStatus == "AwaitingMentor").MinBy(a => a.CreatedAtUtc);
                var confirmed = x.AuditLogs.Where(a => a.NewStatus == "Confirmed").MinBy(a => a.CreatedAtUtc);
                return paid is not null && confirmed is not null && confirmed.CreatedAtUtc >= paid.CreatedAtUtc ? (double?)(confirmed.CreatedAtUtc - paid.CreatedAtUtc).TotalMinutes : null;
            }).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
            var completed = history.Where(x => x.Status == "Completed").ToArray();
            ratings.TryGetValue(mentor.Id, out var rating);
            output.Add(new MetricInput(mentor.Id, mentor.UserId, mentorSkills.Where(x => x.MentorProfileId == mentor.Id && skills.Value.ContainsKey(x.SkillId)).Select(x => x.SkillId).Distinct().ToArray(),
                services.Where(x => x.MentorId == mentor.Id && x.MentorUserId == mentor.UserId).Select(x => new ServicePrice(x.Price, x.Currency)).ToArray(),
                history.Length, accepted.Length, completed.Length, responseTimes.Length > 0 ? responseTimes.Average() : null,
                completed.GroupBy(x => x.MenteeUserId).Count(g => g.Count() >= 2), sessions.GetValueOrDefault(mentor.Id), rating?.Average, rating?.Count ?? 0));
        }
        return output;
    }
}
