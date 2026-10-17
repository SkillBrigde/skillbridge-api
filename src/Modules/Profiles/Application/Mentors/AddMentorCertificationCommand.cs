using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.DTOs;
using SkillBridge.Modules.Profiles.Domain;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record AddMentorCertificationCommand(
    Guid UserId,
    string Name,
    string IssuingOrganization,
    DateTimeOffset IssueDate,
    DateTimeOffset? ExpiryDate,
    string? CredentialUrl,
    string? CredentialId) : ICommand<MentorCertificationDto>;

public sealed class AddMentorCertificationCommandValidator : AbstractValidator<AddMentorCertificationCommand>
{
    public AddMentorCertificationCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Tên chứng chỉ không được chứa ký tự điều khiển.");
        RuleFor(command => command.IssuingOrganization).NotEmpty().MaximumLength(200)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Tổ chức cấp không được chứa ký tự điều khiển.");
        RuleFor(command => command.IssueDate).NotEqual(default(DateTimeOffset));
        RuleFor(command => command)
            .Must(command => !command.ExpiryDate.HasValue || command.ExpiryDate.Value >= command.IssueDate)
            .WithName("expiryDate")
            .WithMessage("Ngày hết hạn phải lớn hơn hoặc bằng ngày cấp.");
        RuleFor(command => command.CredentialUrl).MaximumLength(2048)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("URL chứng chỉ không được chứa ký tự điều khiển.")
            .Must(value => string.IsNullOrWhiteSpace(value) ||
                Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            .WithMessage("URL chứng chỉ phải là URL HTTP hoặc HTTPS hợp lệ.");
        RuleFor(command => command.CredentialId).MaximumLength(200)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Mã chứng chỉ không được chứa ký tự điều khiển.");
    }
}

public sealed class AddMentorCertificationCommandHandler(ProfilesDbContext dbContext)
    : ICommandHandler<AddMentorCertificationCommand, MentorCertificationDto>
{
    public async Task<Result<MentorCertificationDto>> Handle(
        AddMentorCertificationCommand request,
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
                "Hãy tạo hồ sơ Mentor trước khi thêm chứng chỉ.");
        }

        var created = MentorCertification.Create(
            mentorProfileId.Value,
            request.Name,
            request.IssuingOrganization,
            request.IssueDate,
            request.ExpiryDate,
            request.CredentialUrl,
            request.CredentialId);
        if (created.IsFailure)
        {
            return created.Error;
        }

        dbContext.MentorCertifications.Add(created.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MentorCertificationDto.From(created.Value);
    }
}
