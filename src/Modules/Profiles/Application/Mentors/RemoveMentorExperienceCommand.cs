using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record RemoveMentorExperienceCommand(Guid UserId, Guid ExperienceId) : ICommand;

public sealed class RemoveMentorExperienceCommandValidator : AbstractValidator<RemoveMentorExperienceCommand>
{
    public RemoveMentorExperienceCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.ExperienceId).NotEmpty();
    }
}

public sealed class RemoveMentorExperienceCommandHandler(ProfilesDbContext dbContext)
    : ICommandHandler<RemoveMentorExperienceCommand>
{
    public async Task<Result> Handle(
        RemoveMentorExperienceCommand request,
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

        dbContext.MentorExperiences.Remove(experience);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
