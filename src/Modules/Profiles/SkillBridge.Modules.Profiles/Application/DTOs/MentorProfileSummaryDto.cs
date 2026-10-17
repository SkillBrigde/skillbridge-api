namespace SkillBridge.Modules.Profiles.Application.DTOs;

public sealed record MentorProfileSummaryDto(
    Guid Id,
    string Headline,
    decimal HourlyRate,
    string Currency,
    decimal RatingAverage,
    int TotalReviews,
    string VerificationStatus,
    string? AvatarUrl);
