using System.Text.Json.Serialization;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Learning.Domain;

namespace SkillBridge.Modules.Learning.Application;

public sealed record BookedLearningWindow(Guid BookingSessionId, int SessionNumber, DateTimeOffset Start, DateTimeOffset End);
public sealed record LearningBooking(Guid BookingId, Guid MentorId, Guid MentorUserId, Guid MenteeUserId, string Status, IReadOnlyList<BookedLearningWindow> Sessions);
public interface ILearningBookingDirectory
{
    Task<Result<LearningBooking>> GetAsync(Guid bookingId, CancellationToken ct);
}
internal sealed class UnavailableLearningBookings : ILearningBookingDirectory
{
    public Task<Result<LearningBooking>> GetAsync(Guid bookingId, CancellationToken ct) => Task.FromResult<Result<LearningBooking>>(LearningErrors.Dependency);
}
public static class LearningErrors
{
    public static readonly Error Missing = Error.NotFound("Learning.NotFound", "Không tìm thấy buổi học.");
    public static readonly Error Dependency = Error.Failure("Learning.DependencyUnavailable", "Nguồn Booking chưa khả dụng.");
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StartSessionRequest(string? RoomUrl);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NoteRequest(string? Content, bool? IsPrivate);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActionItemRequest(Guid AssignedTo, string? Title, string? Description, DateTimeOffset? DueDate);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActionCompletionRequest(bool? IsCompleted);

public sealed record LearningSessionResponse(Guid Id, Guid BookingId, Guid BookingSessionId, int SessionNumber, Guid MentorId,
    Guid MenteeId, string Status, string? RoomUrl, DateTimeOffset ScheduledStartUtc, DateTimeOffset ScheduledEndUtc,
    DateTimeOffset? ActualStartUtc, DateTimeOffset? ActualEndUtc)
{
    internal static LearningSessionResponse From(LearningSession session) => new(session.Id, session.BookingId, session.BookingSessionId,
        session.SessionNumber, session.MentorId, session.MenteeUserId, session.Status, session.RoomUrl,
        session.ScheduledStartUtc, session.ScheduledEndUtc, session.ActualStartUtc, session.ActualEndUtc);
}
public sealed record NoteResponse(Guid Id, Guid AuthorId, string Content, bool IsPrivate, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record ActionItemResponse(Guid Id, Guid AssignedTo, string Title, string Description, DateTimeOffset? DueDate, bool IsCompleted, DateTimeOffset? CompletedAtUtc);
public sealed record MaterialResponse(Guid Id, Guid UploadedBy, string FileName, string ContentType, long FileSizeBytes, string Sha256, string DownloadUrl, DateTimeOffset CreatedAtUtc);
public sealed record MaterialDownload(byte[] Data, string FileName, string ContentType);
