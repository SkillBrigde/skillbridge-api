using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Profiles.Domain;

public sealed class MentorCertification : AggregateRoot<Guid>
{
    private MentorCertification() { }

    public Guid MentorProfileId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string IssuingOrganization { get; private set; } = string.Empty;
    public DateTimeOffset IssueDate { get; private set; }
    public DateTimeOffset? ExpiryDate { get; private set; }
    public string? CredentialUrl { get; private set; }
    public string? CredentialId { get; private set; }

    public static Result<MentorCertification> Create(
        Guid mentorProfileId,
        string? name,
        string? issuingOrganization,
        DateTimeOffset issueDate,
        DateTimeOffset? expiryDate,
        string? credentialUrl,
        string? credentialId)
    {
        if (mentorProfileId == Guid.Empty)
        {
            return Error.Validation("Profiles.InvalidMentorProfileId", "Mã hồ sơ Mentor không hợp lệ.");
        }

        if (!ProfileInput.IsValidText(name, 200))
        {
            return Error.Validation(
                "Profiles.InvalidCertificationName",
                "Tên chứng chỉ là bắt buộc, tối đa 200 ký tự và không chứa ký tự điều khiển.");
        }

        if (!ProfileInput.IsValidText(issuingOrganization, 200))
        {
            return Error.Validation(
                "Profiles.InvalidIssuingOrganization",
                "Tổ chức cấp là bắt buộc, tối đa 200 ký tự và không chứa ký tự điều khiển.");
        }

        if (issueDate == default)
        {
            return Error.Validation("Profiles.InvalidCertificationIssueDate", "Ngày cấp không hợp lệ.");
        }

        if (expiryDate.HasValue && expiryDate.Value < issueDate)
        {
            return Error.Validation(
                "Profiles.InvalidCertificationDateRange",
                "Ngày hết hạn phải lớn hơn hoặc bằng ngày cấp.");
        }

        if (!ProfileInput.IsValidOptionalHttpUrl(credentialUrl))
        {
            return Error.Validation(
                "Profiles.InvalidCredentialUrl",
                "URL chứng chỉ phải là URL HTTP hoặc HTTPS hợp lệ và không vượt quá 2048 ký tự.");
        }

        if (!ProfileInput.IsValidText(credentialId, 200, false))
        {
            return Error.Validation(
                "Profiles.InvalidCredentialId",
                "Mã chứng chỉ tối đa 200 ký tự và không chứa ký tự điều khiển.");
        }

        return new MentorCertification
        {
            Id = Guid.CreateVersion7(),
            MentorProfileId = mentorProfileId,
            Name = name!.Trim(),
            IssuingOrganization = issuingOrganization!.Trim(),
            IssueDate = issueDate.ToUniversalTime(),
            ExpiryDate = expiryDate?.ToUniversalTime(),
            CredentialUrl = string.IsNullOrWhiteSpace(credentialUrl) ? null : credentialUrl.Trim(),
            CredentialId = string.IsNullOrWhiteSpace(credentialId) ? null : credentialId.Trim()
        };
    }
}
