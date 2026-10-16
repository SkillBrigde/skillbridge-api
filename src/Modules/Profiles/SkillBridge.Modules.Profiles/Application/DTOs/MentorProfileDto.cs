using SkillBridge.Modules.Profiles.Domain;

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
    DateTimeOffset? UpdatedAtUtc)
{
    internal static MentorProfileDto From(
        MentorProfile profile,
        IReadOnlyList<MentorSkillDto>? skills = null,
        IReadOnlyList<MentorExperienceDto>? experiences = null,
        IReadOnlyList<MentorCertificationDto>? certifications = null) => new(
        profile.Id,
        profile.Headline,
        profile.Bio,
        profile.HourlyRate,
        profile.Currency,
        profile.Timezone,
        profile.RatingAverage,
        profile.TotalReviews,
        profile.TotalSessions,
        profile.VerificationStatus,
        profile.IsAcceptingMentees,
        profile.AvatarUrl,
        skills ?? [],
        experiences ?? [],
        certifications ?? [],
        profile.CreatedAtUtc,
        profile.UpdatedAtUtc);
}

public sealed record MentorSkillDto(
    Guid Id,
    Guid SkillId,
    string Name,
    string ProficiencyLevel,
    int YearsOfExperience)
{
    internal static MentorSkillDto From(MentorSkill skill) => new(
        skill.Id,
        skill.SkillId,
        skill.SkillName,
        skill.ProficiencyLevel,
        skill.YearsOfExperience);
}

public sealed record MentorExperienceDto(
    Guid Id,
    string Company,
    string Position,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    string? Description,
    bool IsCurrent)
{
    internal static MentorExperienceDto From(MentorExperience experience) => new(
        experience.Id,
        experience.CompanyName,
        experience.Position,
        experience.StartDate,
        experience.EndDate,
        experience.Description,
        experience.IsCurrent);
}

public sealed record MentorCertificationDto(
    Guid Id,
    string Name,
    string IssuingOrganization,
    DateTimeOffset IssueDate,
    DateTimeOffset? ExpiryDate,
    string? CredentialUrl,
    string? CredentialId);
