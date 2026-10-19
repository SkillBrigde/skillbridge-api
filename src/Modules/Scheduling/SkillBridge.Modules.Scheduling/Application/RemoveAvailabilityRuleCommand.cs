using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Modules.Scheduling.Application;

public sealed record RemoveAvailabilityRuleCommand(Guid UserId, Guid RuleId) : ICommand;

public sealed class RemoveAvailabilityRuleCommandValidator : AbstractValidator<RemoveAvailabilityRuleCommand>
{
    public RemoveAvailabilityRuleCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RuleId).NotEmpty();
    }
}

public sealed class RemoveAvailabilityRuleCommandHandler(
    SchedulingDbContext dbContext,
    AvailabilityRuleStore ruleStore,
    IMentorDirectory mentorDirectory) : ICommandHandler<RemoveAvailabilityRuleCommand>
{
    public async Task<Result> Handle(RemoveAvailabilityRuleCommand request, CancellationToken cancellationToken)
    {
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

        dbContext.AvailabilityRules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
