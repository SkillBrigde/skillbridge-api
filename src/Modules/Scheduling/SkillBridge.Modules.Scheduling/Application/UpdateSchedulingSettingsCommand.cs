using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkillBridge.BuildingBlocks.CQRS;
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
            .InclusiveBetween(15, 240)
            .Must(value => value % 15 == 0)
            .WithMessage("Thời lượng buổi học phải là bội số của 15 phút.");
        RuleFor(command => command.BufferTimeMinutes)
            .InclusiveBetween(0, 120)
            .Must(value => value % 5 == 0)
            .WithMessage("Thời gian nghỉ phải là bội số của 5 phút.");
        RuleFor(command => command.LeadTimeHours).InclusiveBetween(0, 720);
        RuleFor(command => command.MaxDaysInAdvance).InclusiveBetween(1, 365);
        RuleFor(command => command)
            .Must(command => command.LeadTimeHours <= command.MaxDaysInAdvance * 24)
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

        var settings = await dbContext.SchedulingSettings
            .SingleOrDefaultAsync(item => item.MentorId == mentorId.Value, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (settings is null)
        {
            var created = Domain.SchedulingSettings.Create(
                mentorId.Value,
                request.SessionDurationMinutes,
                request.BufferTimeMinutes,
                request.LeadTimeHours,
                request.MaxDaysInAdvance,
                request.Timezone,
                now);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            dbContext.SchedulingSettings.Add(created.Value);
        }
        else
        {
            var updated = settings.Update(
                request.SessionDurationMinutes,
                request.BufferTimeMinutes,
                request.LeadTimeHours,
                request.MaxDaysInAdvance,
                request.Timezone,
                now);
            if (updated.IsFailure)
            {
                return updated;
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Result.Failure(Error.Conflict(
                "Scheduling.SettingsAlreadyExist",
                "Cài đặt lịch vừa được tạo bởi một yêu cầu khác. Vui lòng gửi lại cập nhật."));
        }

        return Result.Success();
    }
}
