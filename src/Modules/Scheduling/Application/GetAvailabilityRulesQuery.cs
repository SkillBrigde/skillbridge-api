using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Scheduling.Application.DTOs.Responses;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Modules.Scheduling.Application;

public sealed record GetAvailabilityRulesQuery(Guid UserId, int Page = 1, int PageSize = 100)
    : IQuery<PagedResult<AvailabilityRuleResponse>>;

public sealed class GetAvailabilityRulesQueryValidator : AbstractValidator<GetAvailabilityRulesQuery>
{
    public GetAvailabilityRulesQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query)
            .Must(query => ((long)query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithName("page")
            .WithMessage("Vị trí trang không được vượt quá Int32.MaxValue.");
    }
}

public sealed class GetAvailabilityRulesQueryHandler(
    SchedulingDbContext dbContext,
    IMentorDirectory mentorDirectory) : IQueryHandler<GetAvailabilityRulesQuery, PagedResult<AvailabilityRuleResponse>>
{
    public async Task<Result<PagedResult<AvailabilityRuleResponse>>> Handle(
        GetAvailabilityRulesQuery request,
        CancellationToken cancellationToken)
    {
        var mentorId = await mentorDirectory.GetMentorIdAsync(request.UserId, cancellationToken);
        if (mentorId.IsFailure)
        {
            return mentorId.Error;
        }

        var query = dbContext.AvailabilityRules.AsNoTracking()
            .Where(rule => rule.MentorId == mentorId.Value);
        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderBy(rule => rule.DayOfWeek)
            .ThenBy(rule => rule.StartTime).ThenBy(rule => rule.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(rule => new AvailabilityRuleResponse(
                rule.Id, rule.MentorId, rule.DayOfWeek, rule.StartTime, rule.EndTime, rule.IsActive))
            .ToListAsync(cancellationToken);

        return PagedResult<AvailabilityRuleResponse>.Create(items, request.Page, request.PageSize, totalCount);
    }
}
