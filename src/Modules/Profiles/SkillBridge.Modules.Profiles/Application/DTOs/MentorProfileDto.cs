namespace SkillBridge.Modules.Profiles.Application.DTOs;

public sealed record MentorProfileDto(
    Guid Id,
    string Headline,
    string Bio,
    decimal HourlyRate,
    string Currency,
    string Timezone,
    decimal RatingAverage,
    int TotalReviews,
    int TotalSessions,
    string VerificationStatus,
    bool IsAcceptingMentees,
    string? AvatarUrl,
    IReadOnlyList<MentorSkillDto> Skills,
    IReadOnlyList<MentorExperienceDto> Experiences,
    IReadOnlyList<MentorCertificationDto> Certifications,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record MentorSkillDto(
    Guid Id,
    Guid SkillId,
    string Name,
    string ProficiencyLevel,
    int YearsOfExperience);

public sealed record MentorExperienceDto(
    Guid Id,
    string Company,
    string Position,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    string? Description,
    bool IsCurrent);

public sealed record MentorCertificationDto(
    Guid Id,
    string Name,
    string IssuingOrganization,
    DateTimeOffset IssueDate,
    DateTimeOffset? ExpiryDate,
    string? CredentialUrl,
    string? CredentialId);
