using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Application.DTOs;
using SkillBridge.Modules.Booking.Infrastructure;

namespace SkillBridge.Modules.Booking.Application;

// Producer chỉ đọc cho adapter Host; không kéo các dependency thanh toán/Learning của command service.
public sealed class BookingSnapshots(BookingDbContext db)
{
    public async Task<Result<BookingResponse>> GetAsync(Guid bookingId, CancellationToken ct)
    {
        var row = await db.Bookings.AsNoTracking().Include(x => x.Sessions).Include(x => x.RescheduleRequests)
            .AsSplitQuery().SingleOrDefaultAsync(x => x.Id == bookingId, ct);
        return row is null ? BookingErrors.Missing : BookingResponse.From(row);
    }
}
