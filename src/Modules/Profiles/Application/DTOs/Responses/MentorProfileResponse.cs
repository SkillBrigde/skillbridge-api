using SkillBridge.Modules.Profiles.Domain;

namespace SkillBridge.Modules.Profiles.Application.DTOs.Responses;

public sealed record MentorProfileResponse(
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
    IReadOnlyList<MentorSkillResponse> Skills,
    IReadOnlyList<MentorExperienceResponse> Experiences,
    IReadOnlyList<MentorCertificationResponse> Certifications,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc)
{
    internal static MentorProfileResponse From(
        MentorProfile profile,
        IReadOnlyList<MentorSkillResponse>? skills = null,
        IReadOnlyList<MentorExperienceResponse>? experiences = null,
        IReadOnlyList<MentorCertificationResponse>? certifications = null) => new(
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
