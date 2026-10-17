using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record RemoveMentorCertificationCommand(Guid UserId, Guid CertificationId) : ICommand;

public sealed class RemoveMentorCertificationCommandValidator : AbstractValidator<RemoveMentorCertificationCommand>
{
    public RemoveMentorCertificationCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.CertificationId).NotEmpty();
    }
}

public sealed class RemoveMentorCertificationCommandHandler(ProfilesDbContext dbContext)
    : ICommandHandler<RemoveMentorCertificationCommand>
{
    public async Task<Result> Handle(
        RemoveMentorCertificationCommand request,
        CancellationToken cancellationToken)
    {
        var certification = await (
            from item in dbContext.MentorCertifications
            join profile in dbContext.MentorProfiles on item.MentorProfileId equals profile.Id
            where profile.UserId == request.UserId && item.Id == request.CertificationId
            select item)
            .SingleOrDefaultAsync(cancellationToken);

        if (certification is null)
        {
            return Result.Failure(Error.NotFound(
                "Profiles.MentorCertificationNotFound",
                "Không tìm thấy chứng chỉ trong hồ sơ Mentor hiện tại."));
        }

        dbContext.MentorCertifications.Remove(certification);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
