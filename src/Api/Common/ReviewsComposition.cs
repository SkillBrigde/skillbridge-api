using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Domain;
using SkillBridge.Modules.Booking.Infrastructure;
using SkillBridge.Modules.Learning.Infrastructure;
using SkillBridge.Modules.Reviews.Application;

namespace SkillBridge.Api.Common;

public static class ReviewsComposition
{
    public static IServiceCollection AddReviewsComposition(this IServiceCollection services) => services.AddScoped<IReviewEvidence, ReviewEvidence>();
}
internal sealed class ReviewEvidence(BookingDbContext bookings, LearningDbContext learning, TimeProvider clock) : IReviewEvidence
{
    public async Task<Result<ReviewEligibility>> GetAsync(Guid bookingId, CancellationToken ct)
    {
        var row = await bookings.Bookings.AsNoTracking().Include(x => x.Sessions).SingleOrDefaultAsync(x => x.Id == bookingId, ct);
        if (row is null) return Error.NotFound("Reviews.BookingNotFound", "Không tìm thấy booking.");
        if (row.Status != "Completed") return Error.Conflict("Reviews.PackageIncomplete", "Cần hoàn tất tất cả buổi và complete cả gói Booking trước khi review.");
        return await EligibleAsync(row, ct);
    }
    public async Task<Result<PagedResult<ReviewEligibility>>> PendingAsync(Guid user, IReadOnlyList<Guid> reviewed, int page, int size, CancellationToken ct)
    {
        var query = bookings.Bookings.AsNoTracking().Where(x => x.MenteeUserId == user && x.Status == "Completed" && !reviewed.Contains(x.Id));
        var count = await query.LongCountAsync(ct);
        var rows = await query.Include(x => x.Sessions).OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var items = new List<ReviewEligibility>();
        foreach (var row in rows)
        {
            var eligible = await EligibleAsync(row, ct);
            if (eligible.IsFailure) return eligible.Error;
            items.Add(eligible.Value);
        }
        return PagedResult<ReviewEligibility>.Create(items, page, size, count);
    }
    private async Task<Result<ReviewEligibility>> EligibleAsync(BookingOrder row, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var sessions = await learning.Sessions.AsNoTracking().Where(x => x.BookingId == row.Id && x.Status == "Completed" && x.ActualEndUtc != null && x.ActualEndUtc <= now)
            .OrderBy(x => x.SessionNumber).Select(x => new { x.Id, x.BookingSessionId }).ToListAsync(ct);
        if (sessions.Count != row.Terms.TotalSessions || !sessions.Select(x => x.BookingSessionId).ToHashSet().SetEquals(row.Sessions.Select(x => x.Id)))
            return Error.Conflict("Reviews.PackageIncomplete", "Chưa có đủ bằng chứng hoàn tất từ Learning.");
        return new ReviewEligibility(row.Id, row.Terms.MentorId, row.MentorUserId, row.MenteeUserId, sessions.Select(x => x.Id).ToArray());
    }
}
