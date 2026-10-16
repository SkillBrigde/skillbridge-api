using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.DTOs;
using SkillBridge.Modules.Profiles.Domain;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record SearchPublicMentorsQuery(
    string? SearchTerm,
    Guid? SkillId,
    decimal? MinRate,
    decimal? MaxRate,
    string? VerificationStatus,
    decimal? MinRating,
    string SortBy,
    bool SortDesc,
    int Page,
    int PageSize) : IQuery<PagedResult<MentorProfileSummaryDto>>;

public sealed class SearchPublicMentorsQueryValidator : AbstractValidator<SearchPublicMentorsQuery>
{
    private static readonly string[] VerificationStatuses = ["Unverified", "Pending", "Verified"];
    private static readonly string[] SortFields = ["rating", "rate", "reviews"];

    public SearchPublicMentorsQueryValidator()
    {
        RuleFor(query => query.SearchTerm)
            .MaximumLength(200)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Từ khóa tối đa 200 ký tự và không chứa ký tự điều khiển.");
        RuleFor(query => query.SkillId)
            .Must(value => value != Guid.Empty)
            .When(query => query.SkillId.HasValue)
            .WithMessage("Skill ID không hợp lệ.");
        RuleFor(query => query.MinRate).GreaterThanOrEqualTo(0).When(query => query.MinRate.HasValue);
        RuleFor(query => query.MaxRate).GreaterThanOrEqualTo(0).When(query => query.MaxRate.HasValue);
        RuleFor(query => query)
            .Must(query => !query.MinRate.HasValue || !query.MaxRate.HasValue || query.MinRate <= query.MaxRate)
            .WithName("maxRate")
            .WithMessage("maxRate phải lớn hơn hoặc bằng minRate.");
        RuleFor(query => query.VerificationStatus)
            .Must(value => value is null || VerificationStatuses.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("verificationStatus chỉ có thể là Unverified, Pending hoặc Verified.");
        RuleFor(query => query.MinRating)
            .InclusiveBetween(0, 5)
            .When(query => query.MinRating.HasValue);
        RuleFor(query => query.SortBy)
            .Must(value => SortFields.Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage("sortBy chỉ có thể là rating, rate hoặc reviews.");
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query)
            .Must(query => ((long)query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithName("page")
            .WithMessage("Vị trí trang không được vượt quá Int32.MaxValue.");
    }
}

public sealed class SearchPublicMentorsQueryHandler(ProfilesDbContext dbContext)
    : IQueryHandler<SearchPublicMentorsQuery, PagedResult<MentorProfileSummaryDto>>
{
    public async Task<Result<PagedResult<MentorProfileSummaryDto>>> Handle(
        SearchPublicMentorsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SkillId.HasValue)
        {
            return Error.Validation(
                "Profiles.SkillFilterUnavailable",
                "Bộ lọc skillId sẽ khả dụng sau khi Mentor Skills được triển khai.");
        }

        var query = dbContext.MentorProfiles
            .AsNoTracking()
            .Where(profile => profile.VerificationStatus != "Rejected");

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var escaped = request.SearchTerm.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var pattern = $"%{escaped}%";
            query = query.Where(profile =>
                EF.Functions.ILike(profile.Headline, pattern, "\\") ||
                EF.Functions.ILike(profile.Bio, pattern, "\\"));
        }

        if (request.MinRate.HasValue)
        {
            query = query.Where(profile => profile.HourlyRate >= request.MinRate.Value);
        }

        if (request.MaxRate.HasValue)
        {
            query = query.Where(profile => profile.HourlyRate <= request.MaxRate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.VerificationStatus))
        {
            var status = request.VerificationStatus.Trim().ToLowerInvariant();
            query = query.Where(profile => profile.VerificationStatus.ToLower() == status);
        }

        if (request.MinRating.HasValue)
        {
            query = query.Where(profile => profile.RatingAverage >= request.MinRating.Value);
        }

        var totalCount = await query.LongCountAsync(cancellationToken);
        var ordered = Order(query, request.SortBy, request.SortDesc);
        var offset = (request.Page - 1) * request.PageSize;
        var items = await ordered
            .Skip(offset)
            .Take(request.PageSize)
            .Select(profile => new MentorProfileSummaryDto(
                profile.Id,
                profile.Headline,
                profile.HourlyRate,
                profile.Currency,
                profile.RatingAverage,
                profile.TotalReviews,
                profile.VerificationStatus,
                profile.AvatarUrl))
            .ToListAsync(cancellationToken);

        return PagedResult<MentorProfileSummaryDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    private static IOrderedQueryable<MentorProfile> Order(
        IQueryable<MentorProfile> query,
        string sortBy,
        bool sortDesc) =>
        (sortBy.ToLowerInvariant(), sortDesc) switch
        {
            ("rate", false) => query.OrderBy(profile => profile.HourlyRate).ThenBy(profile => profile.Id),
            ("rate", true) => query.OrderByDescending(profile => profile.HourlyRate).ThenBy(profile => profile.Id),
            ("reviews", false) => query.OrderBy(profile => profile.TotalReviews).ThenBy(profile => profile.Id),
            ("reviews", true) => query.OrderByDescending(profile => profile.TotalReviews).ThenBy(profile => profile.Id),
            ("rating", false) => query.OrderBy(profile => profile.RatingAverage).ThenBy(profile => profile.Id),
            _ => query.OrderByDescending(profile => profile.RatingAverage).ThenBy(profile => profile.Id)
        };
}
