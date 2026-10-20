using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Learning.Domain;
using SkillBridge.Modules.Learning.Infrastructure;

namespace SkillBridge.Modules.Learning.Application;

public sealed class LearningSessions(LearningDbContext db, ILearningBookingDirectory bookings, TimeProvider clock)
{
    public const int MaxMaterialBytes = 10 * 1024 * 1024;

    public async Task<Result> ConsumeBookingAsync(Guid eventId, Guid bookingId, CancellationToken ct)
    {
        if (eventId == Guid.Empty) return Result.Failure(Error.Validation("Learning.InvalidEvent", "Event ID không hợp lệ."));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(bookingId, ct);
        var receipt = await db.Inbox.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct);
        if (receipt is not null) return receipt.BookingId == bookingId ? Result.Success() : Result.Failure(Error.Conflict("Learning.EventMismatch", "Event đã gắn với booking khác."));
        var latest = await bookings.GetAsync(bookingId, ct);
        if (latest.IsFailure) return Result.Failure(latest.Error);
        if (latest.Value.BookingId != bookingId) return Result.Failure(Error.Conflict("Learning.EventMismatch", "Nguồn trả sai booking."));
        var result = await SyncAsync(latest.Value, ct);
        if (result.IsFailure) return result;
        db.Inbox.Add(new LearningReceipt { Id = eventId, BookingId = bookingId, ProcessedAtUtc = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyCollection<Guid>>> CompletedSessionsAsync(Guid bookingId, CancellationToken ct) =>
        await db.Sessions.AsNoTracking().Where(x => x.BookingId == bookingId && x.Status == "Completed" && x.ActualEndUtc != null)
            .Select(x => x.BookingSessionId).ToListAsync(ct);

    public async Task<Result<PagedResult<LearningSessionResponse>>> ListAsync(Guid user, bool admin, Guid? bookingId, string? status, int page, int size, CancellationToken ct)
    {
        var offset = Offset(page, size); if (offset.IsFailure) return offset.Error;
        if (status is not null && status is not ("Scheduled" or "InProgress" or "Completed" or "Cancelled"))
            return Error.Validation("Learning.InvalidStatus", "Trạng thái buổi học không hợp lệ.");
        var query = db.Sessions.AsNoTracking().Where(x => admin || x.MentorUserId == user || x.MenteeUserId == user);
        if (bookingId.HasValue) query = query.Where(x => x.BookingId == bookingId);
        if (status is not null) query = query.Where(x => x.Status == status);
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(x => x.ScheduledStartUtc).ThenBy(x => x.Id).Skip(offset.Value).Take(size).ToListAsync(ct);
        return PagedResult<LearningSessionResponse>.Create(rows.Select(LearningSessionResponse.From).ToList(), page, size, count);
    }

    public async Task<Result<LearningSessionResponse>> GetAsync(Guid id, Guid user, bool admin, CancellationToken ct)
    {
        var session = await AuthorizeAsync(id, user, admin, ct);
        return session.IsFailure ? session.Error : LearningSessionResponse.From(session.Value);
    }

    public Task<Result<bool>> StartAsync(Guid id, Guid user, string? url, CancellationToken ct) => MutateAsync(id, user, session =>
    {
        var old = session.Status;
        var result = session.Start(user, url, clock.GetUtcNow());
        if (result.IsFailure) return Result.Failure<bool>(result.Error);
        if (old != session.Status) Event("Started", session);
        return Result.Success(true);
    }, ct);

    public Task<Result<bool>> EndAsync(Guid id, Guid user, CancellationToken ct) => MutateAsync(id, user, async (session, token) =>
    {
        var rows = await db.Attendances.Where(x => x.SessionId == id).ToListAsync(token);
        var old = session.Status;
        var result = session.End(user, rows, clock.GetUtcNow());
        if (result.IsFailure) return Result.Failure<bool>(result.Error);
        if (old != session.Status) Event("Completed", session);
        return Result.Success(true);
    }, ct);

    public Task<Result<bool>> JoinAsync(Guid id, Guid user, CancellationToken ct) => MutateAsync(id, user, async (session, token) =>
    {
        if (await db.Attendances.AnyAsync(x => x.SessionId == id && x.UserId == user && x.LeftAtUtc == null, token)) return Result.Success(true);
        if (await db.Attendances.CountAsync(x => x.SessionId == id && x.UserId == user, token) >= 100)
            return Result.Failure<bool>(Error.Conflict("Learning.AttendanceLimit", "Tối đa 100 lượt tham gia mỗi người trong một buổi."));
        var result = SessionAttendance.Join(session, user, clock.GetUtcNow());
        if (result.IsFailure) return Result.Failure<bool>(result.Error);
        db.Attendances.Add(result.Value); return Result.Success(true);
    }, ct);

    public Task<Result<bool>> LeaveAsync(Guid id, Guid user, CancellationToken ct) => MutateAsync(id, user, async (_, token) =>
    {
        var row = await db.Attendances.SingleOrDefaultAsync(x => x.SessionId == id && x.UserId == user && x.LeftAtUtc == null, token);
        row?.Leave(clock.GetUtcNow()); return Result.Success(true);
    }, ct);

    public async Task<Result<PagedResult<NoteResponse>>> NotesAsync(Guid id, Guid user, bool admin, int page, int size, CancellationToken ct)
    {
        var offset = Offset(page, size); if (offset.IsFailure) return offset.Error;
        var session = await AuthorizeAsync(id, user, admin, ct); if (session.IsFailure) return session.Error;
        var query = db.Notes.AsNoTracking().Where(x => x.SessionId == id && (!x.IsPrivate || x.AuthorId == user));
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(offset.Value).Take(size)
            .Select(x => new NoteResponse(x.Id, x.AuthorId, x.Content, x.IsPrivate, x.CreatedAtUtc, x.UpdatedAtUtc)).ToListAsync(ct);
        return PagedResult<NoteResponse>.Create(items, page, size, count);
    }

    public Task<Result<NoteResponse>> AddNoteAsync(Guid id, Guid user, NoteRequest input, CancellationToken ct) => MutateAsync(id, user, session =>
    {
        if (!input.IsPrivate.HasValue) return Result.Failure<NoteResponse>(Error.Validation("Learning.MissingPrivacy", "isPrivate là trường bắt buộc."));
        var result = SessionNote.Create(session.Id, user, input.Content, input.IsPrivate.Value, clock.GetUtcNow());
        if (result.IsFailure) return Result.Failure<NoteResponse>(result.Error);
        var row = result.Value; db.Notes.Add(row);
        return Result.Success(new NoteResponse(row.Id, row.AuthorId, row.Content, row.IsPrivate, row.CreatedAtUtc, row.UpdatedAtUtc));
    }, ct);

    public Task<Result<bool>> UpdateNoteAsync(Guid id, Guid noteId, Guid user, NoteRequest input, CancellationToken ct) => MutateAsync(id, user, async (_, token) =>
    {
        var note = await db.Notes.SingleOrDefaultAsync(x => x.Id == noteId && x.SessionId == id && x.AuthorId == user, token);
        if (note is null) return Result.Failure<bool>(LearningErrors.Missing);
        if (!input.IsPrivate.HasValue) return Result.Failure<bool>(Error.Validation("Learning.MissingPrivacy", "isPrivate là trường bắt buộc."));
        var result = note.Update(input.Content, input.IsPrivate.Value, clock.GetUtcNow());
        return result.IsSuccess ? Result.Success(true) : Result.Failure<bool>(result.Error);
    }, ct);

    public Task<Result<bool>> DeleteNoteAsync(Guid id, Guid noteId, Guid user, CancellationToken ct) => MutateAsync(id, user, async (_, token) =>
    {
        var note = await db.Notes.SingleOrDefaultAsync(x => x.Id == noteId && x.SessionId == id && x.AuthorId == user, token);
        if (note is null) return Result.Failure<bool>(LearningErrors.Missing);
        db.Notes.Remove(note); return Result.Success(true);
    }, ct);

    public async Task<Result<PagedResult<ActionItemResponse>>> ActionsAsync(Guid id, Guid user, bool admin, int page, int size, CancellationToken ct)
    {
        var offset = Offset(page, size); if (offset.IsFailure) return offset.Error;
        var session = await AuthorizeAsync(id, user, admin, ct); if (session.IsFailure) return session.Error;
        var query = db.ActionItems.AsNoTracking().Where(x => x.SessionId == id);
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(offset.Value).Take(size)
            .Select(x => new ActionItemResponse(x.Id, x.AssignedTo, x.Title, x.Description, x.DueDate, x.IsCompleted, x.CompletedAtUtc)).ToListAsync(ct);
        return PagedResult<ActionItemResponse>.Create(rows, page, size, count);
    }

    public Task<Result<ActionItemResponse>> AddActionAsync(Guid id, Guid user, ActionItemRequest input, CancellationToken ct) => MutateAsync(id, user, session =>
    {
        var result = SessionActionItem.Create(session, user, input.AssignedTo, input.Title, input.Description, input.DueDate, clock.GetUtcNow());
        if (result.IsFailure) return Result.Failure<ActionItemResponse>(result.Error);
        var row = result.Value; db.ActionItems.Add(row);
        return Result.Success(new ActionItemResponse(row.Id, row.AssignedTo, row.Title, row.Description, row.DueDate, row.IsCompleted, row.CompletedAtUtc));
    }, ct);

    public Task<Result<bool>> CompleteActionAsync(Guid id, Guid itemId, Guid user, bool? complete, CancellationToken ct) => MutateAsync(id, user, async (_, token) =>
    {
        if (!complete.HasValue) return Result.Failure<bool>(Error.Validation("Learning.MissingCompletion", "isCompleted là trường bắt buộc."));
        var row = await db.ActionItems.SingleOrDefaultAsync(x => x.Id == itemId && x.SessionId == id && x.AssignedTo == user, token);
        if (row is null) return Result.Failure<bool>(LearningErrors.Missing);
        var result = row.SetCompleted(user, complete.Value, clock.GetUtcNow());
        return result.IsSuccess ? Result.Success(true) : Result.Failure<bool>(result.Error);
    }, ct);

    public async Task<Result<PagedResult<MaterialResponse>>> MaterialsAsync(Guid id, Guid user, bool admin, int page, int size, CancellationToken ct)
    {
        var offset = Offset(page, size); if (offset.IsFailure) return offset.Error;
        var session = await AuthorizeAsync(id, user, admin, ct); if (session.IsFailure) return session.Error;
        var query = db.Materials.AsNoTracking().Where(x => x.SessionId == id);
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(offset.Value).Take(size)
            .Select(x => new MaterialResponse(x.Id, x.UploadedBy, x.FileName, x.ContentType, x.FileSizeBytes, x.Sha256,
                $"/api/v1/learning/sessions/{id}/materials/{x.Id}/download", x.CreatedAtUtc)).ToListAsync(ct);
        return PagedResult<MaterialResponse>.Create(items, page, size, count);
    }

    public async Task<Result<MaterialDownload>> DownloadAsync(Guid id, Guid materialId, Guid user, bool admin, CancellationToken ct)
    {
        var session = await AuthorizeAsync(id, user, admin, ct); if (session.IsFailure) return session.Error;
        var row = await db.Materials.AsNoTracking().SingleOrDefaultAsync(x => x.Id == materialId && x.SessionId == id, ct);
        return row is null ? LearningErrors.Missing : new MaterialDownload(row.Data, row.FileName, row.ContentType);
    }

    public async Task<Result<MaterialResponse>> UploadAsync(Guid id, Guid user, string name, string? contentType, Stream body, CancellationToken ct)
    {
        var session = await AuthorizeAsync(id, user, false, ct); if (session.IsFailure) return session.Error;
        name = Uri.UnescapeDataString(name);
        if (name.Length is < 1 or > 150 || name.Any(char.IsControl) || name.IndexOfAny(['/', '\\']) >= 0 || name is "." or "..")
            return Error.Validation("Learning.InvalidFileName", "Tên file không hợp lệ.");
        using var buffer = new MemoryStream();
        var chunk = new byte[65536];
        int read;
        while ((read = await body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxMaterialBytes) return Error.Validation("Learning.MaterialTooLarge", "Tài liệu tối đa 10 MiB.");
            buffer.Write(chunk, 0, read);
        }
        var bytes = buffer.ToArray();
        var type = DetectType(name, bytes);
        if (type is null || contentType?.Split(';')[0].Trim().ToLowerInvariant() is { } declared && declared != "application/octet-stream" && declared != type)
            return Error.Validation("Learning.InvalidFileType", "Chỉ nhận PDF, PNG, JPEG hoặc TXT UTF-8 đúng nội dung và MIME.");
        var sha = Convert.ToHexString(SHA256.HashData(bytes));
        return await MutateAsync(id, user, row =>
        {
            var material = SessionMaterial.Create(id, user, name, type, sha, bytes, clock.GetUtcNow());
            db.Materials.Add(material);
            return Result.Success(new MaterialResponse(material.Id, user, name, type, bytes.Length, sha,
                $"/api/v1/learning/sessions/{id}/materials/{material.Id}/download", material.CreatedAtUtc));
        }, ct);
    }

    public Task<Result<bool>> DeleteMaterialAsync(Guid id, Guid materialId, Guid user, CancellationToken ct) => MutateAsync(id, user, async (_, token) =>
    {
        var row = await db.Materials.SingleOrDefaultAsync(x => x.Id == materialId && x.SessionId == id && x.UploadedBy == user, token);
        if (row is null) return Result.Failure<bool>(LearningErrors.Missing);
        db.Materials.Remove(row); return Result.Success(true);
    }, ct);

    private Task<Result<T>> MutateAsync<T>(Guid id, Guid user, Func<LearningSession, Result<T>> action, CancellationToken ct) =>
        MutateAsync(id, user, (session, _) => Task.FromResult(action(session)), ct);

    private async Task<Result<T>> MutateAsync<T>(Guid id, Guid user, Func<LearningSession, CancellationToken, Task<Result<T>>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var bookingId = await db.Sessions.AsNoTracking().Where(x => x.Id == id && (x.MenteeUserId == user || x.MentorUserId == user))
            .Select(x => (Guid?)x.BookingId).SingleOrDefaultAsync(ct);
        if (!bookingId.HasValue) return LearningErrors.Missing;
        await LockAsync(bookingId.Value, ct);
        db.ChangeTracker.Clear();
        var source = await bookings.GetAsync(bookingId.Value, ct);
        if (source.IsFailure) return source.Error;
        if (source.Value.BookingId != bookingId.Value)
            return Error.Conflict("Learning.BookingMismatch", "Nguồn trả sai định danh booking.");
        if (source.Value.Status is not ("Confirmed" or "Completed"))
            return Error.Conflict("Learning.BookingNotConfirmed", "Booking không còn cho phép thay đổi dữ liệu học.");
        var synced = await SyncAsync(source.Value, ct);
        if (synced.IsFailure) return synced.Error;
        var session = await db.Sessions.SingleAsync(x => x.Id == id, ct);
        var result = await action(session, ct);
        if (result.IsFailure) return result;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return result;
    }

    private async Task<Result> SyncAsync(LearningBooking source, CancellationToken ct)
    {
        if (source.BookingId == Guid.Empty || source.MentorId == Guid.Empty || source.MentorUserId == Guid.Empty || source.MenteeUserId == Guid.Empty ||
            source.Sessions.Count > 52 || source.Sessions.Select(x => x.BookingSessionId).Distinct().Count() != source.Sessions.Count ||
            source.Status is not ("Confirmed" or "Completed" or "Cancelled" or "RefundReview") || source.Status is "Confirmed" or "Completed" && source.Sessions.Count == 0)
            return Result.Failure(Error.Conflict("Learning.InvalidBooking", "Booking chưa có đủ dữ liệu học tin cậy."));
        var rows = await db.Sessions.Where(x => x.BookingId == source.BookingId).ToListAsync(ct);
        var cancelled = source.Status is "Cancelled" or "RefundReview";
        if (!cancelled && rows.Any(x => !source.Sessions.Any(w => w.BookingSessionId == x.BookingSessionId)))
            return Result.Failure(Error.Conflict("Learning.PackageChanged", "Không được xóa buổi đã đồng bộ khỏi gói."));
        foreach (var window in source.Sessions)
        {
            var row = rows.SingleOrDefault(x => x.BookingSessionId == window.BookingSessionId);
            if (row is null)
            {
                var created = LearningSession.Create(source.BookingId, window.BookingSessionId, source.MentorId, source.MentorUserId,
                    source.MenteeUserId, window.SessionNumber, window.Start, window.End, clock.GetUtcNow());
                if (created.IsFailure) return Result.Failure(created.Error);
                row = created.Value; db.Sessions.Add(row); rows.Add(row);
            }
            if (row.MentorUserId != source.MentorUserId || row.MenteeUserId != source.MenteeUserId || row.MentorId != source.MentorId)
                return Result.Failure(Error.Conflict("Learning.ParticipantsChanged", "Không được thay người tham gia của gói."));
            var result = row.Sync(window.Start, window.End, cancelled);
            if (result.IsFailure) return result;
        }
        if (cancelled)
        {
            foreach (var row in rows) row.Sync(row.ScheduledStartUtc, row.ScheduledEndUtc, true);
            var ids = rows.Select(x => x.Id).ToArray();
            foreach (var attendance in await db.Attendances.Where(x => ids.Contains(x.SessionId) && x.LeftAtUtc == null).ToListAsync(ct)) attendance.Leave(clock.GetUtcNow());
        }
        return Result.Success();
    }

    private async Task<Result<LearningSession>> AuthorizeAsync(Guid id, Guid user, bool admin, CancellationToken ct)
    {
        var row = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && (admin || x.MentorUserId == user || x.MenteeUserId == user), ct);
        return row is null ? LearningErrors.Missing : row;
    }

    // Cùng khóa "booking:<id>" với Booking; bảo vệ kiểm tra lịch/hoàn tất khi hai module ghi đồng thời.
    private Task LockAsync(Guid bookingId, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock(hashtextextended({"booking:" + bookingId}, 0))", ct);

    private void Event(string kind, LearningSession row) => db.OutboxMessages.Add(new OutboxMessage
    { Type = $"Learning.{kind}.v1", Content = JsonSerializer.Serialize(LearningSessionResponse.From(row)), OccurredOnUtc = clock.GetUtcNow() });

    private static Result<int> Offset(int page, int size) => page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue
        ? Error.Validation("Learning.InvalidPagination", "page tối thiểu 1, pageSize 1–100 và offset không vượt Int32.MaxValue.") : (page - 1) * size;

    private static string? DetectType(string name, byte[] bytes)
    {
        if (bytes.Length == 0) return null;
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension == ".pdf" && bytes.AsSpan().StartsWith("%PDF-"u8)) return "application/pdf";
        if (extension == ".png" && bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (extension is ".jpg" or ".jpeg" && bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return "image/jpeg";
        if (extension != ".txt") return null;
        try { return new UTF8Encoding(false, true).GetString(bytes).Any(x => char.IsControl(x) && x is not ('\r' or '\n' or '\t')) ? null : "text/plain"; }
        catch (DecoderFallbackException) { return null; }
    }
}
