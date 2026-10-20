using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Learning.Domain;

public static class LearningText
{
    public static bool Valid(string? text, int limit, bool required = true) =>
        (!required || !string.IsNullOrWhiteSpace(text)) && (text?.Length ?? 0) <= limit &&
        text?.Any(x => char.IsControl(x) && x is not ('\r' or '\n' or '\t')) != true;
}

public sealed class SessionNote : AggregateRoot<Guid>
{
    private SessionNote() { }
    public Guid SessionId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = "";
    public bool IsPrivate { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public static Result<SessionNote> Create(Guid sessionId, Guid author, string? content, bool isPrivate, DateTimeOffset now)
    {
        if (sessionId == Guid.Empty || author == Guid.Empty || !LearningText.Valid(content, 10000))
            return Error.Validation("Learning.InvalidNote", "Ghi chú bắt buộc, tối đa 10000 ký tự.");
        return new SessionNote
        {
            Id = Guid.CreateVersion7(),
            SessionId = sessionId,
            AuthorId = author,
            Content = content!.Trim(),
            IsPrivate = isPrivate,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
    public Result Update(string? content, bool isPrivate, DateTimeOffset now)
    {
        if (!LearningText.Valid(content, 10000)) return Result.Failure(Error.Validation("Learning.InvalidNote", "Ghi chú bắt buộc, tối đa 10000 ký tự."));
        Content = content!.Trim(); IsPrivate = isPrivate; UpdatedAtUtc = now;
        return Result.Success();
    }
}

public sealed class SessionActionItem : AggregateRoot<Guid>
{
    private SessionActionItem() { }
    public Guid SessionId { get; private set; }
    public Guid AssignedTo { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public DateTimeOffset? DueDate { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public static Result<SessionActionItem> Create(LearningSession session, Guid actor, Guid assignedTo, string? title,
        string? description, DateTimeOffset? due, DateTimeOffset now)
    {
        if (actor != session.MentorUserId) return Error.NotFound("Learning.NotFound", "Không tìm thấy buổi học.");
        if (assignedTo != session.MenteeUserId || !LearningText.Valid(title, 200) || !LearningText.Valid(description, 4000, false) || due < now)
            return Error.Validation("Learning.InvalidAction", "Chỉ giao cho Mentee của buổi, tiêu đề tối đa 200, mô tả 4000 ký tự và hạn không ở quá khứ.");
        return new SessionActionItem
        {
            Id = Guid.CreateVersion7(),
            SessionId = session.Id,
            AssignedTo = assignedTo,
            Title = title!.Trim(),
            Description = description?.Trim() ?? "",
            DueDate = due?.ToUniversalTime(),
            CreatedAtUtc = now
        };
    }
    public Result SetCompleted(Guid actor, bool completed, DateTimeOffset now)
    {
        if (actor != AssignedTo) return Result.Failure(Error.NotFound("Learning.ActionNotFound", "Không tìm thấy bài tập được giao."));
        if (IsCompleted != completed) { IsCompleted = completed; CompletedAtUtc = completed ? now : null; }
        return Result.Success();
    }
}

public sealed class SessionMaterial : AggregateRoot<Guid>
{
    private SessionMaterial() { }
    public Guid SessionId { get; private set; }
    public Guid UploadedBy { get; private set; }
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public string Sha256 { get; private set; } = "";
    public long FileSizeBytes { get; private set; }
    // shortcut: lưu bytea tối đa 10 MiB/tài liệu; chuyển object storage khi lượng tài liệu tăng.
    public byte[] Data { get; private set; } = [];
    public DateTimeOffset CreatedAtUtc { get; private set; }
    internal static SessionMaterial Create(Guid session, Guid author, string name, string type, string sha256, byte[] data, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            SessionId = session,
            UploadedBy = author,
            FileName = name,
            ContentType = type,
            Sha256 = sha256,
            FileSizeBytes = data.LongLength,
            Data = data,
            CreatedAtUtc = now
        };
}
