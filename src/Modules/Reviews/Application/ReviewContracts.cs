using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Reviews.Application;

public sealed record ReviewEligibility(Guid BookingId, Guid MentorId, Guid MentorUserId, Guid MenteeUserId, IReadOnlyList<Guid> CompletedSessionIds);
public interface IReviewEvidence
{
    Task<Result<ReviewEligibility>> GetAsync(Guid bookingId, CancellationToken ct);
    Task<Result<PagedResult<ReviewEligibility>>> PendingAsync(Guid user, IReadOnlyList<Guid> reviewed, int page, int size, CancellationToken ct);
}
internal sealed class UnavailableReviewEvidence : IReviewEvidence
{
    private static readonly Error Missing = Error.Failure("Reviews.EvidenceUnavailable", "Nguồn Booking/Learning chưa khả dụng.");
    public Task<Result<ReviewEligibility>> GetAsync(Guid bookingId, CancellationToken ct) => Task.FromResult<Result<ReviewEligibility>>(Missing);
    public Task<Result<PagedResult<ReviewEligibility>>> PendingAsync(Guid user, IReadOnlyList<Guid> reviewed, int page, int size, CancellationToken ct) => Task.FromResult<Result<PagedResult<ReviewEligibility>>>(Missing);
}
public sealed record CreateReviewRequest(Guid BookingId, Guid SessionId, Guid MentorId, int OverallRating, int CommunicationRating, int ExpertiseRating, int HelpfulnessRating, string? Comment);
public sealed record UpdateReviewRequest(int OverallRating, string? Comment, int? CommunicationRating, int? ExpertiseRating, int? HelpfulnessRating);
public sealed record ReplyReviewRequest(string? Reply);
// Không công khai userId Mentee, bookingId hay sessionId qua API Anonymous.
public sealed record PublicReview(Guid Id, Guid MentorId, int OverallRating, int CommunicationRating, int ExpertiseRating,
    int HelpfulnessRating, string Comment, string? MentorReply, DateTimeOffset? MentorRepliedAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record RatingSummary(Guid MentorId, double AverageOverall, double AverageCommunication, double AverageExpertise, double AverageHelpfulness, long TotalReviews);
