using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Catalog.Domain;

public sealed class MentorService : AggregateRoot<Guid>
{
    private MentorService() { }
    public Guid MentorId { get; private set; }
    public Guid MentorUserId { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string ServiceType { get; private set; } = "";
    public int DurationMinutes { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "";
    public int TotalSessions { get; private set; }
    public string Deliverables { get; private set; } = "";
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static Result<MentorService> Create(Guid mentorId, Guid mentorUserId, string? title,
        string? description, string? type, int duration, decimal price, string? currency, int sessions,
        string? deliverables, bool active, DateTimeOffset now)
    {
        if (mentorId == Guid.Empty || mentorUserId == Guid.Empty)
            return Error.Validation("Catalog.InvalidMentor", "Hồ sơ và người dùng Mentor phải hợp lệ.");
        var service = new MentorService
        {
            Id = Guid.CreateVersion7(),
            MentorId = mentorId,
            MentorUserId = mentorUserId,
            CreatedAtUtc = now
        };
        var result = service.SetTerms(title, description, type, duration, price, currency, sessions, deliverables, active);
        return result.IsFailure ? result.Error : service;
    }

    public Result Update(string? title, string? description, string? type, int duration, decimal price,
        string? currency, int sessions, string? deliverables, bool active, DateTimeOffset now)
    {
        var result = SetTerms(title, description, type, duration, price, currency, sessions, deliverables, active);
        if (result.IsFailure) return result;
        Version++;
        UpdatedAtUtc = now;
        return Result.Success();
    }

    public void Delete(DateTimeOffset now)
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAtUtc = now;
        Version++;
    }

    private Result SetTerms(string? title, string? description, string? type, int duration, decimal price,
        string? currency, int sessions, string? deliverables, bool active)
    {
        var normalizedCurrency = currency?.Trim().ToUpperInvariant();
        if (!MentorServiceRules.Text(title, 200) || !MentorServiceRules.Text(description, 4000, true) ||
            !MentorServiceRules.Text(deliverables, 4000, true) || !MentorServiceRules.Type(type) ||
            duration is < 15 or > 240 || sessions is < 1 or > 52 || !MentorServiceRules.Price(price, normalizedCurrency))
            return Result.Failure(Error.Validation("Catalog.InvalidService", "Nội dung, tier, thời lượng, giá hoặc số buổi không hợp lệ."));
        Title = title!.Trim(); Description = description!.Trim(); ServiceType = type!;
        DurationMinutes = duration; Price = price; Currency = normalizedCurrency!;
        TotalSessions = sessions; Deliverables = deliverables!.Trim(); IsActive = active;
        return Result.Success();
    }
}

public static class MentorServiceRules
{
    public static bool Type(string? value) => value is "Tier1_ReviewCV" or "Tier2_MockInterview" or "Tier3_Roadmap";
    public static bool Text(string? value, int max, bool multiline = false) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max &&
        !value.Any(c => char.IsControl(c) && !(multiline && c is '\r' or '\n' or '\t'));
    public static bool Price(decimal price, string? currency) => currency switch
    {
        "VND" => price is > 0 and <= 100_000_000 && price == decimal.Truncate(price),
        "USD" => price is > 0 and <= 5_000 && price == decimal.Round(price, 2),
        _ => false
    };
}
