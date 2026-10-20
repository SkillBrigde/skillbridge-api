using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Domain;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record UpdateMentorVerificationCommand(
    Guid MentorId,
    string? VerificationStatus) : ICommand;

public sealed class UpdateMentorVerificationCommandValidator
    : AbstractValidator<UpdateMentorVerificationCommand>
{
    public UpdateMentorVerificationCommandValidator()
    {
        RuleFor(command => command.MentorId).NotEmpty();
        RuleFor(command => command.VerificationStatus)
            .Must(MentorVerificationStatuses.IsSupported)
            .WithMessage("Trạng thái xác minh chỉ có thể là Unverified, Pending hoặc Verified.");
    }
}

public sealed class UpdateMentorVerificationCommandHandler(
    ProfilesDbContext dbContext,
    TimeProvider timeProvider) : ICommandHandler<UpdateMentorVerificationCommand>
{
    public async Task<Result> Handle(
        UpdateMentorVerificationCommand request,
        CancellationToken cancellationToken)
    {
        var mentor = await dbContext.MentorProfiles
            .SingleOrDefaultAsync(profile => profile.Id == request.MentorId, cancellationToken);
        if (mentor is null)
        {
            return Result.Failure(Error.NotFound(
                "Profiles.MentorNotFound",
                "Không tìm thấy hồ sơ Mentor."));
        }

        var updated = mentor.UpdateVerificationStatus(
            request.VerificationStatus,
            timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return updated;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
