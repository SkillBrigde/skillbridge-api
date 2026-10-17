using FluentValidation;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Scheduling.Domain;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Modules.Scheduling.Application;

public sealed record AddAvailabilityRuleCommand(
    Guid UserId, int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime) : ICommand<AvailabilityRuleDto>;

public sealed class AddAvailabilityRuleCommandValidator : AbstractValidator<AddAvailabilityRuleCommand>
{
    public AddAvailabilityRuleCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command).Custom((command, context) =>
        {
            var window = AvailabilityWindow.Create(command.DayOfWeek, command.StartTime, command.EndTime);
            if (window.IsFailure)
            {
                context.AddFailure("availability", window.Error.Description);
            }
        });
    }
}

public sealed class AddAvailabilityRuleCommandHandler(
    SchedulingDbContext dbContext,
    AvailabilityRuleStore ruleStore,
    IMentorDirectory mentorDirectory,
    TimeProvider timeProvider) : ICommandHandler<AddAvailabilityRuleCommand, AvailabilityRuleDto>
{
    public async Task<Result<AvailabilityRuleDto>> Handle(
        AddAvailabilityRuleCommand request,
        CancellationToken cancellationToken)
    {
        var window = AvailabilityWindow.Create(request.DayOfWeek, request.StartTime, request.EndTime);
        if (window.IsFailure)
        {
            return window.Error;
        }

        var mentorId = await mentorDirectory.GetMentorIdAsync(request.UserId, cancellationToken);
        if (mentorId.IsFailure)
        {
            return mentorId.Error;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await ruleStore.LockSettingsAsync(mentorId.Value, cancellationToken))
        {
            return AvailabilityRuleErrors.SettingsRequired;
        }

        if (await ruleStore.HasOverlapAsync(mentorId.Value, window.Value, null, cancellationToken))
        {
            return AvailabilityRuleErrors.Overlap;
        }

        var created = AvailabilityRule.Create(mentorId.Value, window.Value, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error;
        }

        dbContext.AvailabilityRules.Add(created.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AvailabilityRuleDto.From(created.Value);
    }
}
