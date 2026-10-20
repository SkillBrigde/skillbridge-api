using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Reviews.Domain;

public sealed class Review : AggregateRoot<Guid>
{
    private Review() { }
    public Guid BookingId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid MentorId { get; private set; }
    public Guid MentorUserId { get; private set; }
    public Guid MenteeUserId { get; private set; }
    public int OverallRating { get; private set; }
    public int CommunicationRating { get; private set; }
    public int ExpertiseRating { get; private set; }
    public int HelpfulnessRating { get; private set; }
    public string Comment { get; private set; } = "";
    public string? MentorReply { get; private set; }
    public DateTimeOffset? MentorRepliedAtUtc { get; private set; }
    public bool IsVisible { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public static Result<Review> Create(Guid booking, Guid session, Guid mentor, Guid mentorUser, Guid mentee,
        int overall, int communication, int expertise, int helpfulness, string? comment, DateTimeOffset now)
    {
        if (new[] { booking, session, mentor, mentorUser, mentee }.Contains(Guid.Empty)) return Error.Validation("Reviews.InvalidReference", "Thiếu định danh review.");
        var review = new Review
        {
            Id = Guid.CreateVersion7(),
            BookingId = booking,
            SessionId = session,
            MentorId = mentor,
            MentorUserId = mentorUser,
            MenteeUserId = mentee,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var valid = review.SetRatings(overall, communication, expertise, helpfulness, comment);
        return valid.IsFailure ? valid.Error : review;
    }
    public Result Update(Guid actor, int overall, int? communication, int? expertise, int? helpfulness, string? comment, DateTimeOffset now)
    {
        if (actor != MenteeUserId) return Result.Failure(Error.NotFound("Reviews.NotFound", "Không tìm thấy review."));
        if (now < CreatedAtUtc || now >= CreatedAtUtc.AddHours(48)) return Result.Failure(Error.Conflict("Reviews.EditClosed", "Đã hết 48 giờ sửa review."));
        var valid = SetRatings(overall, communication ?? CommunicationRating, expertise ?? ExpertiseRating, helpfulness ?? HelpfulnessRating, comment);
        if (valid.IsSuccess) UpdatedAtUtc = now;
        return valid;
    }
    public Result Reply(Guid actor, string? reply, DateTimeOffset now)
    {
        if (actor != MentorUserId) return Result.Failure(Error.NotFound("Reviews.NotFound", "Không tìm thấy review."));
        if (!Text(reply, 4000)) return Result.Failure(Error.Validation("Reviews.InvalidReply", "Phản hồi bắt buộc, tối đa 4000 ký tự."));
        MentorReply = reply!.Trim(); MentorRepliedAtUtc = now; UpdatedAtUtc = now; return Result.Success();
    }
    private Result SetRatings(int overall, int communication, int expertise, int helpfulness, string? comment)
    {
        if (new[] { overall, communication, expertise, helpfulness }.Any(x => x is < 1 or > 5) || !Text(comment, 4000))
            return Result.Failure(Error.Validation("Reviews.InvalidRatings", "Cả bốn điểm phải từ 1–5; nhận xét bắt buộc, tối đa 4000 ký tự."));
        OverallRating = overall; CommunicationRating = communication; ExpertiseRating = expertise; HelpfulnessRating = helpfulness;
        Comment = comment!.Trim(); return Result.Success();
    }
    private static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
}
