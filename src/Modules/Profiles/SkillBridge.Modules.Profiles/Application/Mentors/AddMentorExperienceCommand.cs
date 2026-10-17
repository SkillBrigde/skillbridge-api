using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.DTOs;
using SkillBridge.Modules.Profiles.Domain;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record AddMentorExperienceCommand(
    Guid UserId,
    string Company,
    string Position,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    string? Description,
    bool IsCurrent) : ICommand<MentorExperienceDto>;

public sealed class AddMentorExperienceCommandValidator : AbstractValidator<AddMentorExperienceCommand>
{
    public AddMentorExperienceCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
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

public sealed class AddMentorExperienceCommandHandler(ProfilesDbContext dbContext)
    : ICommandHandler<AddMentorExperienceCommand, MentorExperienceDto>
{
    public async Task<Result<MentorExperienceDto>> Handle(
        AddMentorExperienceCommand request,
        CancellationToken cancellationToken)
    {
        var mentorProfileId = await dbContext.MentorProfiles
            .Where(profile => profile.UserId == request.UserId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!mentorProfileId.HasValue)
        {
            return Error.Conflict(
                "Profiles.MentorProfileRequired",
                "Hãy tạo hồ sơ Mentor trước khi thêm kinh nghiệm.");
        }

        var created = MentorExperience.Create(
            mentorProfileId.Value,
            request.Company,
            request.Position,
            request.StartDate,
            request.EndDate,
            request.Description,
            request.IsCurrent);
        if (created.IsFailure)
        {
            return created.Error;
        }

        dbContext.MentorExperiences.Add(created.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MentorExperienceDto.From(created.Value);
    }
}
