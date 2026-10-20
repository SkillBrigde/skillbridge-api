using SkillBridge.BuildingBlocks.Domain;

namespace SkillBridge.Modules.Recommendations.Domain;

public sealed record ServicePrice(decimal Amount, string Currency);
public sealed class MentorMetric : AggregateRoot<Guid>
{
    private MentorMetric() { }
    public Guid MentorUserId { get; private set; }
    public Guid[] SkillIds { get; private set; } = [];
    public ServicePrice[] Prices { get; private set; } = [];
    public double? AcceptanceRate { get; private set; }
    public double? CompletionRate { get; private set; }
    public double? AverageResponseTimeMinutes { get; private set; }
    public int RepeatMenteeCount { get; private set; }
    public int TotalSessions { get; private set; }
    public double? AverageRating { get; private set; }
    public int TotalReviews { get; private set; }
    public DateTimeOffset LastCalculatedAtUtc { get; private set; }
    public static MentorMetric Create(Guid mentor) => new() { Id = mentor };
    public void Update(Guid owner, Guid[] skills, ServicePrice[] prices, int paid, int accepted, int completed,
        double? responseMinutes, int repeats, int sessions, double? rating, int reviews, DateTimeOffset now)
    {
        MentorUserId = owner; SkillIds = skills.Distinct().Order().ToArray(); Prices = prices;
        AcceptanceRate = paid > 0 ? Math.Clamp((double)accepted / paid, 0, 1) : null;
        CompletionRate = accepted > 0 ? Math.Clamp((double)completed / accepted, 0, 1) : null;
        AverageResponseTimeMinutes = responseMinutes; RepeatMenteeCount = repeats; TotalSessions = sessions;
        AverageRating = rating; TotalReviews = reviews; LastCalculatedAtUtc = now;
    }
}
public sealed class MenteeInteraction : AggregateRoot<Guid>
{
    public Guid UserId { get; init; }
    public Guid MentorId { get; init; }
    public Guid? SkillId { get; init; }
    public string InteractionType { get; init; } = "";
    public string DeduplicationKey { get; init; } = "";
    public DateTimeOffset CreatedAtUtc { get; init; }
    public MenteeInteraction() { Id = Guid.CreateVersion7(); }
}
public sealed class RecommendationLog : AggregateRoot<Guid>
{
    public Guid UserId { get; init; }
    public string AlgorithmVersion { get; init; } = "rules-v1";
    public Guid[] MentorIds { get; init; } = [];
    public double[] Scores { get; init; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; }
    public RecommendationLog() { Id = Guid.CreateVersion7(); }
}
public static class RecommendationScoring
{
    public static (double Score, IReadOnlyList<string> Reasons) Calculate(MentorMetric mentor, IReadOnlyDictionary<Guid, int> interests,
        decimal? budgetMin, decimal? budgetMax, string? currency)
    {
        var total = interests.Values.Sum();
        var overlap = total > 0 ? mentor.SkillIds.Where(interests.ContainsKey).Sum(x => interests[x]) / (double)total : 0;
        var budget = budgetMin.HasValue && budgetMax.HasValue && budgetMax > 0 && mentor.Prices.Any(x => x.Currency == currency && x.Amount >= budgetMin && x.Amount <= budgetMax);
        var score = 0.45 * overlap + 0.20 * (mentor.AverageRating ?? 0) / 5 + 0.10 * (mentor.CompletionRate ?? 0) +
            0.10 * (mentor.AcceptanceRate ?? 0) + 0.05 * (mentor.AverageResponseTimeMinutes.HasValue ? 1 / (1 + mentor.AverageResponseTimeMinutes.Value / 60) : 0) + (budget ? 0.10 : 0);
        var reasons = new List<string>();
        if (overlap > 0) reasons.Add("Có kỹ năng bạn đã quan tâm trong 90 ngày qua");
        if (mentor.AverageRating >= 4) reasons.Add("Điểm đánh giá từ 4/5 trở lên từ review đã xác minh");
        if (mentor.TotalSessions > 0) reasons.Add($"Đã hoàn tất {mentor.TotalSessions} buổi học");
        if (budget) reasons.Add("Có gói dịch vụ cùng loại tiền, trong ngân sách hồ sơ của bạn");
        if (reasons.Count == 0) reasons.Add("Mentor đang nhận học viên và có dịch vụ hoạt động; chưa đủ lịch sử để cá nhân hóa");
        return (Math.Round(Math.Clamp(score, 0, 1), 6), reasons);
    }
}
