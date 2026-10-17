using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Profiles.Domain;

public sealed class MentorExperience : AggregateRoot<Guid>
{
    private MentorExperience() { }

    public Guid MentorProfileId { get; private set; }
    public string CompanyName { get; private set; } = string.Empty;
    public string Position { get; private set; } = string.Empty;
    public DateTimeOffset StartDate { get; private set; }
    public DateTimeOffset? EndDate { get; private set; }
    public string? Description { get; private set; }
    public bool IsCurrent { get; private set; }

    public static Result<MentorExperience> Create(
        Guid mentorProfileId,
        string? companyName,
        string? position,
        DateTimeOffset startDate,
        DateTimeOffset? endDate,
        string? description,
        bool isCurrent)
    {
        if (mentorProfileId == Guid.Empty)
        {
            return Error.Validation("Profiles.InvalidMentorProfileId", "Mã hồ sơ Mentor không hợp lệ.");
        }

        var experience = new MentorExperience
        {
            Id = Guid.CreateVersion7(),
            MentorProfileId = mentorProfileId
        };
        var updated = experience.Update(companyName, position, startDate, endDate, description, isCurrent);
        return updated.IsFailure ? updated.Error : experience;
    }

    public Result Update(
        string? companyName,
        string? position,
        DateTimeOffset startDate,
        DateTimeOffset? endDate,
        string? description,
        bool isCurrent)
    {
        if (!ProfileInput.IsValidText(companyName, 200))
        {
            return Result.Failure(Error.Validation(
                "Profiles.InvalidExperienceCompany",
                "Tên công ty là bắt buộc, tối đa 200 ký tự và không chứa ký tự điều khiển."));
        }

        if (!ProfileInput.IsValidText(position, 200))
        {
            return Result.Failure(Error.Validation(
                "Profiles.InvalidExperiencePosition",
                "Vị trí là bắt buộc, tối đa 200 ký tự và không chứa ký tự điều khiển."));
        }

        if (!ProfileInput.IsValidText(description, 4000, false))
        {
            return Result.Failure(Error.Validation(
                "Profiles.InvalidExperienceDescription",
                "Mô tả tối đa 4000 ký tự và không chứa ký tự điều khiển."));
        }

        if (startDate == default)
        {
            return Result.Failure(Error.Validation("Profiles.InvalidExperienceStartDate", "Ngày bắt đầu không hợp lệ."));
        }

        if (isCurrent && endDate.HasValue || !isCurrent && !endDate.HasValue)
        {
            return Result.Failure(Error.Validation(
                "Profiles.InvalidExperienceCurrentState",
                "Công việc hiện tại không có ngày kết thúc; công việc đã kết thúc phải có ngày kết thúc."));
        }

        if (endDate.HasValue && endDate.Value < startDate)
        {
            return Result.Failure(Error.Validation(
                "Profiles.InvalidExperienceDateRange",
                "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu."));
        }

        CompanyName = companyName!.Trim();
        Position = position!.Trim();
        StartDate = startDate.ToUniversalTime();
        EndDate = endDate?.ToUniversalTime();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsCurrent = isCurrent;
        return Result.Success();
    }
}
