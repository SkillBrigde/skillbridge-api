using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Scheduling.Domain;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Modules.Scheduling.Application;

public sealed record UpdateAvailabilityRuleCommand(
    Guid UserId, Guid RuleId, int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, bool IsActive) : ICommand;

public sealed class UpdateAvailabilityRuleCommandValidator : AbstractValidator<UpdateAvailabilityRuleCommand>
{
    public UpdateAvailabilityRuleCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RuleId).NotEmpty();
    }
}

public sealed class UpdateAvailabilityRuleCommandHandler(
    SchedulingDbContext dbContext,
    AvailabilityRuleStore ruleStore,
    IMentorDirectory mentorDirectory,
    TimeProvider timeProvider) : ICommandHandler<UpdateAvailabilityRuleCommand>
{
    public async Task<Result> Handle(UpdateAvailabilityRuleCommand request, CancellationToken cancellationToken)
    {
        var window = AvailabilityWindow.Create(request.DayOfWeek, request.StartTime, request.EndTime);
        if (window.IsFailure)
        {
            return Result.Failure(window.Error);
        }

        var mentorId = await mentorDirectory.GetMentorIdAsync(request.UserId, cancellationToken);
        if (mentorId.IsFailure)
        {
            return Result.Failure(mentorId.Error);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await ruleStore.LockSettingsAsync(mentorId.Value, cancellationToken))
        {
            return Result.Failure(AvailabilityRuleErrors.SettingsRequired);
        }

        var rule = await dbContext.AvailabilityRules.SingleOrDefaultAsync(
            item => item.Id == request.RuleId && item.MentorId == mentorId.Value, cancellationToken);
        if (rule is null)
        {
            return Result.Failure(AvailabilityRuleErrors.NotFound);
        }

        if (request.IsActive && await ruleStore.HasOverlapAsync(
            mentorId.Value, window.Value, request.RuleId, cancellationToken))
        {
            return Result.Failure(AvailabilityRuleErrors.Overlap);
        }

        rule.Update(window.Value, request.IsActive, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
