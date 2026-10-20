using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Scheduling.Domain;
using SkillBridge.Modules.Scheduling.Infrastructure;

namespace SkillBridge.Modules.Scheduling.Application;

public sealed record UpdateSchedulingSettingsCommand(
    Guid UserId,
    int SessionDurationMinutes,
    int BufferTimeMinutes,
    int LeadTimeHours,
    int MaxDaysInAdvance,
    string Timezone) : ICommand;

public sealed class UpdateSchedulingSettingsCommandValidator : AbstractValidator<UpdateSchedulingSettingsCommand>
{
    public UpdateSchedulingSettingsCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.SessionDurationMinutes)
            .Must(SchedulingSettingsRules.IsValidSessionDuration)
            .WithMessage("Thời lượng buổi học phải từ 15 đến 240 phút và là bội số của 15.");
        RuleFor(command => command.BufferTimeMinutes)
            .Must(SchedulingSettingsRules.IsValidBufferTime)
            .WithMessage("Thời gian nghỉ phải từ 0 đến 120 phút và là bội số của 5.");
        RuleFor(command => command.LeadTimeHours)
            .Must(SchedulingSettingsRules.IsValidLeadTime)
            .WithMessage("Thời gian đặt trước phải từ 0 đến 720 giờ.");
        RuleFor(command => command.MaxDaysInAdvance)
            .Must(SchedulingSettingsRules.IsValidAdvanceWindow)
            .WithMessage("Số ngày cho phép đặt trước phải từ 1 đến 365 ngày.");
        RuleFor(command => command)
            .Must(command => SchedulingSettingsRules.IsValidBookingWindow(
                command.LeadTimeHours,
                command.MaxDaysInAdvance))
            .WithName("leadTimeHours")
            .WithMessage("Thời gian đặt trước tối thiểu không được vượt quá cửa sổ cho phép đặt lịch.");
        RuleFor(command => command.Timezone)
            .Must(SchedulingSettingsRules.IsValidTimezone)
            .WithMessage("Múi giờ phải là mã IANA hợp lệ, ví dụ Asia/Ho_Chi_Minh, hoặc UTC.");
    }
}

public sealed class UpdateSchedulingSettingsCommandHandler(
    SchedulingDbContext dbContext,
    IMentorDirectory mentorDirectory,
    TimeProvider timeProvider) : ICommandHandler<UpdateSchedulingSettingsCommand>
{
    public async Task<Result> Handle(
        UpdateSchedulingSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var mentorId = await mentorDirectory.GetMentorIdAsync(request.UserId, cancellationToken);
        if (mentorId.IsFailure)
        {
            return Result.Failure(mentorId.Error);
        }

        var values = SchedulingSettingsValues.Create(
            request.SessionDurationMinutes,
            request.BufferTimeMinutes,
            request.LeadTimeHours,
            request.MaxDaysInAdvance,
            request.Timezone);
        if (values.IsFailure)
        {
            return Result.Failure(values.Error);
        }

        var settings = await dbContext.SchedulingSettings
            .SingleOrDefaultAsync(item => item.MentorId == mentorId.Value, cancellationToken);
        var now = timeProvider.GetUtcNow();
        SchedulingSettings? createdSettings = null;

        if (settings is null)
        {
            var created = Domain.SchedulingSettings.Create(
                mentorId.Value,
                values.Value,
                now);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            createdSettings = created.Value;
            dbContext.SchedulingSettings.Add(createdSettings);
        }
        else
        {
            settings.Update(values.Value, now);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            createdSettings is not null &&
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_scheduling_settings_mentor_id"
            })
        {
            dbContext.Entry(createdSettings).State = EntityState.Detached;

            var concurrentSettings = await dbContext.SchedulingSettings
                .SingleAsync(item => item.MentorId == mentorId.Value, cancellationToken);
            concurrentSettings.Update(values.Value, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
