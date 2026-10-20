using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Reviews.Domain;
using SkillBridge.Modules.Reviews.Infrastructure;

namespace SkillBridge.Modules.Reviews.Application;

public sealed class ReviewsService(ReviewsDbContext db, IReviewEvidence evidence, TimeProvider clock)
{
    private static readonly Error Missing = Error.NotFound("Reviews.NotFound", "Không tìm thấy review.");
    public async Task<Result<PublicReview>> CreateAsync(Guid user, CreateReviewRequest request, CancellationToken ct)
    {
        if (request.BookingId == Guid.Empty || request.MentorId == Guid.Empty || request.SessionId == Guid.Empty)
            return Error.Validation("Reviews.InvalidRequest", "Thiếu booking, session hoặc mentor.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync("booking:" + request.BookingId, ct); db.ChangeTracker.Clear();
        var eligible = await evidence.GetAsync(request.BookingId, ct);
        if (eligible.IsFailure) return eligible.Error;
        if (eligible.Value.MenteeUserId != user) return Missing;
        if (eligible.Value.MentorId != request.MentorId || !eligible.Value.CompletedSessionIds.Contains(request.SessionId))
            return Error.Conflict("Reviews.EvidenceMismatch", "Mentor/buổi học không thuộc gói đã hoàn tất.");
        if (await db.Reviews.AnyAsync(x => x.BookingId == request.BookingId, ct))
            return Error.Conflict("Reviews.AlreadyExists", "Mỗi booking chỉ có một review.");
        var created = Review.Create(request.BookingId, request.SessionId, eligible.Value.MentorId, eligible.Value.MentorUserId, user,
            request.OverallRating, request.CommunicationRating, request.ExpertiseRating, request.HelpfulnessRating, request.Comment, clock.GetUtcNow());
        if (created.IsFailure) return created.Error;
        db.Reviews.Add(created.Value); Record(created.Value, "Reviews.Submitted.v1");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(created.Value);
    }
    public async Task<Result<PublicReview>> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Reviews.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsVisible, ct);
        return row is null ? Missing : Map(row);
    }
    public async Task<Result> UpdateAsync(Guid id, Guid user, UpdateReviewRequest request, CancellationToken ct) =>
        await MutateAsync(id, user, false, x => x.Update(user, request.OverallRating, request.CommunicationRating, request.ExpertiseRating, request.HelpfulnessRating, request.Comment, clock.GetUtcNow()), ct);
    public async Task<Result> ReplyAsync(Guid id, Guid user, string? reply, CancellationToken ct) =>
        await MutateAsync(id, user, true, x => x.Reply(user, reply, clock.GetUtcNow()), ct);
    private async Task<Result> MutateAsync(Guid id, Guid user, bool mentor, Func<Review, Result> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(id.ToString(), ct); db.ChangeTracker.Clear();
        var row = await db.Reviews.SingleOrDefaultAsync(x => x.Id == id && x.IsVisible && (mentor ? x.MentorUserId == user : x.MenteeUserId == user), ct);
        if (row is null) return Result.Failure(Missing);
        var result = action(row); if (result.IsFailure) return result;
        Record(row, mentor ? "Reviews.Replied.v1" : "Reviews.Updated.v1");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Result.Success();
    }
    public async Task<Result<PagedResult<PublicReview>>> ListAsync(Guid mentor, int page, int size, CancellationToken ct)
    {
        var offset = Offset(page, size); if (offset.IsFailure) return offset.Error;
        var query = db.Reviews.AsNoTracking().Where(x => x.MentorId == mentor && x.IsVisible);
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(offset.Value).Take(size).ToListAsync(ct);
        return PagedResult<PublicReview>.Create(rows.Select(Map).ToList(), page, size, count);
    }
    public async Task<RatingSummary> SummaryAsync(Guid mentor, CancellationToken ct)
    {
        var summary = await db.Reviews.AsNoTracking().Where(x => x.MentorId == mentor && x.IsVisible).GroupBy(x => x.MentorId)
            .Select(g => new RatingSummary(g.Key, g.Average(x => x.OverallRating), g.Average(x => x.CommunicationRating),
                g.Average(x => x.ExpertiseRating), g.Average(x => x.HelpfulnessRating), g.LongCount())).SingleOrDefaultAsync(ct);
        return summary ?? new RatingSummary(mentor, 0, 0, 0, 0, 0);
    }
    public async Task<Result<PagedResult<ReviewEligibility>>> PendingAsync(Guid user, int page, int size, CancellationToken ct)
    {
        var offset = Offset(page, size); if (offset.IsFailure) return offset.Error;
        var reviewed = await db.Reviews.AsNoTracking().Where(x => x.MenteeUserId == user).Select(x => x.BookingId).ToListAsync(ct);
        return await evidence.PendingAsync(user, reviewed, page, size, ct);
    }
    public static Result<int> Offset(int page, int size) => page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue
        ? Error.Validation("Reviews.InvalidPage", "Phân trang không hợp lệ.") : (page - 1) * size;
    private Task LockAsync(string key, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"reviews:" + key}, 0))", ct);
    private void Record(Review row, string type) => db.OutboxMessages.Add(new OutboxMessage
    {
        Id = Guid.CreateVersion7(),
        Type = type,
        Content = JsonSerializer.Serialize(new { row.Id, row.MentorId, row.BookingId }),
        OccurredOnUtc = clock.GetUtcNow()
    });
    private static PublicReview Map(Review x) => new(x.Id, x.MentorId, x.OverallRating, x.CommunicationRating, x.ExpertiseRating,
        x.HelpfulnessRating, x.Comment, x.MentorReply, x.MentorRepliedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc);
}
