using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Profiles.Domain;

public sealed class MenteeProfile : AggregateRoot<Guid>
{
    private MenteeProfile() { }

    public Guid UserId { get; private set; }
    public string CareerGoal { get; private set; } = string.Empty;
    public string CurrentLevel { get; private set; } = "Beginner";
    public string Timezone { get; private set; } = "Asia/Ho_Chi_Minh";
    public decimal BudgetMin { get; private set; }
    public decimal BudgetMax { get; private set; }
    public string Currency { get; private set; } = "VND";
    public string? AvatarUrl { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static Result<MenteeProfile> Create(
        Guid userId,
        string? careerGoal,
        string? currentLevel,
        string? timezone,
        decimal budgetMin,
        decimal budgetMax,
        string? currency,
        string? avatarUrl,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            return Error.Validation("Profiles.InvalidUserId", "Mã người dùng không hợp lệ.");
        }

        var profile = new MenteeProfile
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CreatedAtUtc = now
        };
        var update = profile.Update(careerGoal, currentLevel, timezone, budgetMin, budgetMax, currency, avatarUrl, now);
        return update.IsFailure ? update.Error : profile;
    }

    public Result Update(
        string? careerGoal,
        string? currentLevel,
        string? timezone,
        decimal budgetMin,
        decimal budgetMax,
        string? currency,
        string? avatarUrl,
        DateTimeOffset now)
    {
        if (!ProfileInput.IsValidText(careerGoal, 2000))
        {
            return Result.Failure(Error.Validation("Profiles.InvalidCareerGoal", "Mục tiêu nghề nghiệp là bắt buộc, tối đa 2000 ký tự và không chứa ký tự điều khiển."));
        }

        var normalizedLevel = currentLevel?.Trim();
        if (normalizedLevel is not ("Beginner" or "Intermediate" or "Advanced"))
        {
            return Result.Failure(Error.Validation("Profiles.InvalidCurrentLevel", "Trình độ chỉ có thể là Beginner, Intermediate hoặc Advanced."));
        }

        if (!ProfileInput.IsValidText(timezone, 100))
        {
            return Result.Failure(Error.Validation("Profiles.InvalidTimezone", "Múi giờ là bắt buộc, tối đa 100 ký tự và không chứa ký tự điều khiển."));
        }

        if (budgetMin < 0 ||
            budgetMax < 0 ||
            budgetMin > ProfileInput.MaximumStoredMoney ||
            budgetMax > ProfileInput.MaximumStoredMoney ||
            budgetMin > budgetMax)
        {
            return Result.Failure(Error.Validation(
                "Profiles.InvalidBudget",
                $"Ngân sách phải từ 0 đến {ProfileInput.MaximumStoredMoney} và budgetMin không được lớn hơn budgetMax."));
        }

        var normalizedCurrency = currency?.Trim().ToUpperInvariant();
        if (!ProfileInput.IsSupportedCurrency(normalizedCurrency))
        {
            return Result.Failure(Error.Validation("Profiles.InvalidCurrency", "Đơn vị tiền tệ chỉ có thể là VND hoặc USD."));
        }

        if (!ProfileInput.IsValidOptionalHttpUrl(avatarUrl))
        {
            return Result.Failure(Error.Validation("Profiles.InvalidAvatarUrl", "Avatar phải là URL HTTP/HTTPS hợp lệ, tối đa 2048 ký tự."));
        }

        CareerGoal = careerGoal!.Trim();
        CurrentLevel = normalizedLevel;
        Timezone = timezone!.Trim();
        BudgetMin = budgetMin;
        BudgetMax = budgetMax;
        Currency = normalizedCurrency!;
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        UpdatedAtUtc = now;
        return Result.Success();
    }
}
