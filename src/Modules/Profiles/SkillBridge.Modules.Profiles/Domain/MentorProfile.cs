using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Profiles.Domain;

public sealed class MentorProfile : AggregateRoot<Guid>
{
    private MentorProfile() { }

    public Guid UserId { get; private set; }
    public string Headline { get; private set; } = string.Empty;
    public string Bio { get; private set; } = string.Empty;
    public decimal HourlyRate { get; private set; }
    public string Currency { get; private set; } = "VND";
    public string Timezone { get; private set; } = "Asia/Ho_Chi_Minh";
    public decimal RatingAverage { get; private set; }
    public int TotalReviews { get; private set; }
    public int TotalSessions { get; private set; }
    public string VerificationStatus { get; private set; } = "Unverified";
    public bool IsAcceptingMentees { get; private set; }
    public string? AvatarUrl { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static Result<MentorProfile> Create(
        Guid userId,
        string? headline,
        string? bio,
        decimal hourlyRate,
        string? currency,
        string? timezone,
        bool isAcceptingMentees,
        string? avatarUrl,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            return Error.Validation("Profiles.InvalidUserId", "Mã người dùng không hợp lệ.");
        }

        var profile = new MentorProfile
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CreatedAtUtc = now
        };
        var update = profile.Update(headline, bio, hourlyRate, currency, timezone, isAcceptingMentees, avatarUrl, now);
        return update.IsFailure ? update.Error : profile;
    }

    public Result Update(
        string? headline,
        string? bio,
        decimal hourlyRate,
        string? currency,
        string? timezone,
        bool isAcceptingMentees,
        string? avatarUrl,
        DateTimeOffset now)
    {
        if (!ProfileInput.IsValidText(headline, 200))
        {
            return Result.Failure(Error.Validation("Profiles.InvalidHeadline", "Tiêu đề hồ sơ Mentor là bắt buộc, tối đa 200 ký tự và không chứa ký tự điều khiển."));
        }

        if (!ProfileInput.IsValidText(bio, 4000))
        {
            return Result.Failure(Error.Validation("Profiles.InvalidBio", "Giới thiệu Mentor là bắt buộc, tối đa 4000 ký tự và không chứa ký tự điều khiển."));
        }

        var normalizedCurrency = currency?.Trim().ToUpperInvariant();
        if (!ProfileInput.IsSupportedCurrency(normalizedCurrency))
        {
            return Result.Failure(Error.Validation("Profiles.InvalidCurrency", "Đơn vị tiền tệ chỉ có thể là VND hoặc USD."));
        }

        var maximumRate = normalizedCurrency == "VND" ? 10_000_000m : 500m;
        if (hourlyRate < 0 || hourlyRate > maximumRate)
        {
            return Result.Failure(Error.Validation("Profiles.InvalidHourlyRate", $"Mức phí phải từ 0 đến {maximumRate} {normalizedCurrency}."));
        }

        if (!ProfileInput.IsValidText(timezone, 100))
        {
            return Result.Failure(Error.Validation("Profiles.InvalidTimezone", "Múi giờ là bắt buộc, tối đa 100 ký tự và không chứa ký tự điều khiển."));
        }

        if (!ProfileInput.IsValidOptionalHttpUrl(avatarUrl))
        {
            return Result.Failure(Error.Validation("Profiles.InvalidAvatarUrl", "Avatar phải là URL HTTP/HTTPS hợp lệ, tối đa 2048 ký tự."));
        }

        Headline = headline!.Trim();
        Bio = bio!.Trim();
        HourlyRate = hourlyRate;
        Currency = normalizedCurrency!;
        Timezone = timezone!.Trim();
        IsAcceptingMentees = isAcceptingMentees;
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        UpdatedAtUtc = now;
        return Result.Success();
    }
}
