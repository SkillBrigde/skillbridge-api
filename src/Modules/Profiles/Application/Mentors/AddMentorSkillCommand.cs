using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.DTOs;
using SkillBridge.Modules.Profiles.Domain;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record AddMentorSkillCommand(
    Guid UserId,
    Guid SkillId,
    string ProficiencyLevel,
    int YearsOfExperience) : ICommand<MentorSkillDto>;

public sealed class AddMentorSkillCommandValidator : AbstractValidator<AddMentorSkillCommand>
{
    private static readonly string[] ProficiencyLevels = ["Beginner", "Intermediate", "Advanced", "Expert"];

    public AddMentorSkillCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.SkillId).NotEmpty();
        RuleFor(command => command.ProficiencyLevel)
            .Must(value => value is not null && ProficiencyLevels.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Trình độ phải là Beginner, Intermediate, Advanced hoặc Expert.");
        RuleFor(command => command.YearsOfExperience).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddMentorSkillCommandHandler(
    ProfilesDbContext dbContext,
    ISkillDirectory skillDirectory,
    TimeProvider timeProvider) : ICommandHandler<AddMentorSkillCommand, MentorSkillDto>
{
    public async Task<Result<MentorSkillDto>> Handle(
        AddMentorSkillCommand request,
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
                "Hãy tạo hồ sơ Mentor trước khi thêm kỹ năng.");
        }

        if (await dbContext.MentorSkills.AnyAsync(
                skill => skill.MentorProfileId == mentorProfileId.Value && skill.SkillId == request.SkillId,
                cancellationToken))
        {
            return Error.Conflict("Profiles.MentorSkillAlreadyExists", "Kỹ năng đã có trong hồ sơ Mentor.");
        }

        var directorySkill = await skillDirectory.GetActiveAsync(request.SkillId, cancellationToken);
        if (directorySkill.IsFailure)
        {
            return directorySkill.Error;
        }

        var created = MentorSkill.Create(
            mentorProfileId.Value,
            directorySkill.Value.Id,
            directorySkill.Value.Name,
            request.ProficiencyLevel,
            request.YearsOfExperience,
            timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error;
        }

        dbContext.MentorSkills.Add(created.Value);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Error.Conflict("Profiles.MentorSkillAlreadyExists", "Kỹ năng đã có trong hồ sơ Mentor.");
        }

        return MentorSkillDto.From(created.Value);
    }
}
