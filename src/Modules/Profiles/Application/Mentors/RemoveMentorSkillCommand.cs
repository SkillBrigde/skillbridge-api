using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record RemoveMentorSkillCommand(Guid UserId, Guid SkillId) : ICommand;

public sealed class RemoveMentorSkillCommandValidator : AbstractValidator<RemoveMentorSkillCommand>
{
    public RemoveMentorSkillCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.SkillId).NotEmpty();
    }
}

public sealed class RemoveMentorSkillCommandHandler(ProfilesDbContext dbContext)
    : ICommandHandler<RemoveMentorSkillCommand>
{
    public async Task<Result> Handle(
        RemoveMentorSkillCommand request,
        CancellationToken cancellationToken)
    {
        var mentorSkill = await (
            from skill in dbContext.MentorSkills
            join profile in dbContext.MentorProfiles on skill.MentorProfileId equals profile.Id
            where profile.UserId == request.UserId && skill.SkillId == request.SkillId
            select skill)
            .SingleOrDefaultAsync(cancellationToken);

        if (mentorSkill is null)
        {
            return Result.Failure(Error.NotFound(
                "Profiles.MentorSkillNotFound",
                "Không tìm thấy kỹ năng trong hồ sơ Mentor hiện tại."));
        }

        dbContext.MentorSkills.Remove(mentorSkill);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
