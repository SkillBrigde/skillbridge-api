namespace SkillBridge.Modules.Profiles.Application.DTOs.Responses;

public sealed record MentorProfileSummaryResponse(
    Guid Id,
    string Headline,
    decimal HourlyRate,
    string Currency,
    decimal RatingAverage,
    int TotalReviews,
    string VerificationStatus,
    string? AvatarUrl);
