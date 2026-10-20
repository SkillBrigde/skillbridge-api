using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Booking.Domain;

public sealed record ServiceTerms(Guid ServiceId, long Version, Guid MentorId, Guid MentorUserId,
    string Title, string Description, string ServiceType, string Deliverables, decimal Price, string Currency,
    int DurationMinutes, int TotalSessions, DateTimeOffset CapturedAtUtc)
{
    public Result Validate() => ServiceId == Guid.Empty || Version < 1 || MentorId == Guid.Empty || MentorUserId == Guid.Empty ||
        string.IsNullOrWhiteSpace(Title) || Title.Length > 200 || Description.Length > 4000 || Deliverables.Length > 4000 ||
        ServiceType.Length > 50 || DurationMinutes is < 15 or > 240 || TotalSessions is < 1 or > 52 ||
        Price <= 0 || Price > 1_000_000_000 || Currency is not ("VND" or "USD") ||
        decimal.Round(Price, Currency == "VND" ? 0 : 2) != Price
            ? Result.Failure(Error.Validation("Booking.InvalidServiceTerms", "Thông tin dịch vụ không hợp lệ.")) : Result.Success();
}

public sealed record IntakeForm(string CoreQuestion, string AttachedLinks, string TargetGoals)
{
    public Result Validate()
    {
        if (!Text(CoreQuestion, 4000) || !Text(TargetGoals, 4000) || !Text(AttachedLinks, 4000))
            return Result.Failure(Error.Validation("Booking.InvalidIntake", "Ba nội dung intake bắt buộc, tối đa 4000 ký tự mỗi nội dung."));
        var links = AttachedLinks.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return links.Length is < 1 or > 10 || links.Any(x => !Uri.TryCreate(x, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            ? Result.Failure(Error.Validation("Booking.InvalidLinks", "Cung cấp 1–10 URL HTTP/HTTPS, phân cách bằng dấu phẩy hoặc xuống dòng.")) : Result.Success();
    }

    public static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max &&
        !value.Any(x => char.IsControl(x) && x is not ('\r' or '\n' or '\t'));
}
