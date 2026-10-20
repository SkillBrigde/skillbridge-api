using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Recommendations.Domain;

namespace SkillBridge.Modules.Recommendations.Application;

public sealed record MetricInput(Guid MentorId, Guid UserId, Guid[] SkillIds, ServicePrice[] Prices, int PaidBookings,
    int AcceptedBookings, int CompletedBookings, double? ResponseMinutes, int RepeatMentees, int CompletedSessions, double? Rating, int Reviews);
public sealed record MenteePreference(decimal? BudgetMin, decimal? BudgetMax, string? Currency);
public interface IRecommendationSources
{
    Task<Result<IReadOnlyList<MetricInput>>> LoadMetricsAsync(CancellationToken ct);
    Task<Result<IReadOnlyList<Guid>>> EligibleMentorsAsync(CancellationToken ct);
    Task<Result<IReadOnlyDictionary<Guid, string>>> SkillsAsync(CancellationToken ct);
    Task<Result<MenteePreference>> PreferenceAsync(Guid user, CancellationToken ct);
    Task<Result<Guid>> MentorOwnerAsync(Guid mentor, CancellationToken ct);
    Task<Result<bool>> HasSkillAsync(Guid mentor, Guid skill, CancellationToken ct);
}
internal sealed class UnavailableRecommendationSources : IRecommendationSources
{
    public Task<Result<IReadOnlyList<MetricInput>>> LoadMetricsAsync(CancellationToken ct) => Task.FromResult<Result<IReadOnlyList<MetricInput>>>(RecommendationErrors.Unavailable);
    public Task<Result<IReadOnlyList<Guid>>> EligibleMentorsAsync(CancellationToken ct) => Task.FromResult<Result<IReadOnlyList<Guid>>>(RecommendationErrors.Unavailable);
    public Task<Result<IReadOnlyDictionary<Guid, string>>> SkillsAsync(CancellationToken ct) => Task.FromResult<Result<IReadOnlyDictionary<Guid, string>>>(RecommendationErrors.Unavailable);
    public Task<Result<MenteePreference>> PreferenceAsync(Guid user, CancellationToken ct) => Task.FromResult<Result<MenteePreference>>(RecommendationErrors.Unavailable);
    public Task<Result<Guid>> MentorOwnerAsync(Guid mentor, CancellationToken ct) => Task.FromResult<Result<Guid>>(RecommendationErrors.Unavailable);
    public Task<Result<bool>> HasSkillAsync(Guid mentor, Guid skill, CancellationToken ct) => Task.FromResult<Result<bool>>(RecommendationErrors.Unavailable);
}
public sealed record InteractionRequest(Guid MentorId, Guid? SkillId, string? InteractionType);
public sealed record MentorMetrics(Guid MentorId, double? AcceptanceRate, double? CompletionRate, double? AverageResponseTimeMinutes,
    int RepeatMenteeCount, int TotalSessions, double? AverageRating, int TotalReviews, DateTimeOffset LastCalculatedAtUtc)
{
    public static MentorMetrics From(MentorMetric x) => new(x.Id, x.AcceptanceRate, x.CompletionRate, x.AverageResponseTimeMinutes,
        x.RepeatMenteeCount, x.TotalSessions, x.AverageRating, x.TotalReviews, x.LastCalculatedAtUtc);
}
public sealed record RecommendedMentor(Guid MentorId, double Score, IReadOnlyList<string> Reasons, PublicMentorMetrics Metrics);
public sealed record PublicMentorMetrics(double? AverageRating, int TotalSessions, double? AcceptanceRate, double? CompletionRate);
public sealed record TrendingSkill(Guid SkillId, string Name, int TrendingScore);
public static class RecommendationErrors
{
    public static readonly Error Unavailable = Error.Failure("Recommendations.Unavailable", "Nguồn dữ liệu hoặc metrics chưa sẵn sàng.");
    public static readonly Error Missing = Error.NotFound("Recommendations.NotFound", "Không tìm thấy Mentor/kỹ năng phù hợp.");
}
