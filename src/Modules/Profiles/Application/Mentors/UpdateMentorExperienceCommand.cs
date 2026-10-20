using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record UpdateMentorExperienceCommand(
    Guid UserId,
    Guid ExperienceId,
    string Company,
    string Position,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    string? Description,
    bool IsCurrent) : ICommand;

public sealed class UpdateMentorExperienceCommandValidator : AbstractValidator<UpdateMentorExperienceCommand>
{
    public UpdateMentorExperienceCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.ExperienceId).NotEmpty();
        RuleFor(command => command.Company).NotEmpty().MaximumLength(200)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Tên công ty không được chứa ký tự điều khiển.");
        RuleFor(command => command.Position).NotEmpty().MaximumLength(200)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Vị trí không được chứa ký tự điều khiển.");
        RuleFor(command => command.Description).MaximumLength(4000)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Mô tả không được chứa ký tự điều khiển.");
        RuleFor(command => command.StartDate).NotEqual(default(DateTimeOffset));
        RuleFor(command => command)
            .Must(command => command.IsCurrent ? !command.EndDate.HasValue : command.EndDate.HasValue)
            .WithName("endDate")
            .WithMessage("Công việc hiện tại không có ngày kết thúc; công việc đã kết thúc phải có ngày kết thúc.");
        RuleFor(command => command)
            .Must(command => !command.EndDate.HasValue || command.EndDate.Value >= command.StartDate)
            .WithName("endDate")
            .WithMessage("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
    }
}

public sealed class UpdateMentorExperienceCommandHandler(ProfilesDbContext dbContext)
    : ICommandHandler<UpdateMentorExperienceCommand>
{
    public async Task<Result> Handle(
        UpdateMentorExperienceCommand request,
        CancellationToken cancellationToken)
    {
        var experience = await (
            from item in dbContext.MentorExperiences
            join profile in dbContext.MentorProfiles on item.MentorProfileId equals profile.Id
            where profile.UserId == request.UserId && item.Id == request.ExperienceId
            select item)
            .SingleOrDefaultAsync(cancellationToken);

        if (experience is null)
        {
            return Result.Failure(Error.NotFound(
                "Profiles.MentorExperienceNotFound",
                "Không tìm thấy kinh nghiệm trong hồ sơ Mentor hiện tại."));
        }

        var updated = experience.Update(
            request.Company,
            request.Position,
            request.StartDate,
            request.EndDate,
            request.Description,
            request.IsCurrent);
        if (updated.IsFailure)
        {
            return updated;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
