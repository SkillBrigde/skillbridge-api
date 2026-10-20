using SkillBridge.Modules.Profiles.Domain;

namespace SkillBridge.Modules.Profiles.Application.DTOs.Responses;

public sealed record MentorCertificationResponse(
    Guid Id,
    string Name,
    string IssuingOrganization,
    DateTimeOffset IssueDate,
    DateTimeOffset? ExpiryDate,
    string? CredentialUrl,
    string? CredentialId)
{
    internal static MentorCertificationResponse From(MentorCertification certification) => new(
        certification.Id,
        certification.Name,
        certification.IssuingOrganization,
        certification.IssueDate,
        certification.ExpiryDate,
        certification.CredentialUrl,
        certification.CredentialId);
}
