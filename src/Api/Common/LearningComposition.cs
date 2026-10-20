using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Booking.Application;
using SkillBridge.Modules.Booking.Application.DTOs;
using SkillBridge.Modules.Learning.Application;

namespace SkillBridge.Api.Common;

public static class LearningComposition
{
    public static IServiceCollection AddLearningComposition(this IServiceCollection services)
    {
        services.AddScoped<ILearningBookingDirectory, BookingLearningDirectory>();
        services.AddScoped<IBookingCompletionEvidence, LearningBookingCompletion>();
        services.AddScoped<IBookingLifecycleConsumer, LearningLifecycleConsumer>();
        services.AddScoped<BookingLifecycleRelay>();
        services.AddHostedService<BookingLifecycleWorker>();
        return services;
    }
    internal static LearningBooking Map(BookingResponse source) => new(source.Id, source.MentorId, source.ServiceSnapshot.MentorUserId,
        source.MenteeUserId, source.Status, source.Sessions.Where(x => x.StartAtUtc.HasValue && x.EndAtUtc.HasValue)
            .Select(x => new BookedLearningWindow(x.Id, x.SessionNumber, x.StartAtUtc!.Value, x.EndAtUtc!.Value)).ToList());
}
internal sealed class BookingLearningDirectory(BookingSnapshots bookings) : ILearningBookingDirectory
{
    public async Task<Result<LearningBooking>> GetAsync(Guid bookingId, CancellationToken ct)
    {
        var result = await bookings.GetAsync(bookingId, ct);
        return result.IsFailure ? result.Error : LearningComposition.Map(result.Value);
    }
}
internal sealed class LearningBookingCompletion(LearningSessions sessions) : IBookingCompletionEvidence
{
    public Task<Result<IReadOnlyCollection<Guid>>> GetCompletedSessionsAsync(Guid bookingId, CancellationToken ct) => sessions.CompletedSessionsAsync(bookingId, ct);
}
internal sealed class LearningLifecycleConsumer(LearningSessions sessions) : IBookingLifecycleConsumer
{
    public Task<Result> ConsumeAsync(Guid eventId, Guid bookingId, CancellationToken ct) => sessions.ConsumeBookingAsync(eventId, bookingId, ct);
}
