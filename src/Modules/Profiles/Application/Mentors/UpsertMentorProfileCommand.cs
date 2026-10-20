using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Domain;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentors;

public sealed record UpsertMentorProfileCommand(
    Guid UserId,
    string Headline,
    string Bio,
    decimal HourlyRate,
    string Currency,
    string Timezone,
    bool IsAcceptingMentees,
    string? AvatarUrl) : ICommand;

public sealed class UpsertMentorProfileCommandValidator : AbstractValidator<UpsertMentorProfileCommand>
{
    public UpsertMentorProfileCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Headline).NotEmpty().MaximumLength(200)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Tiêu đề không được chứa ký tự điều khiển.");
        RuleFor(request => request.Bio).NotEmpty().MaximumLength(4000)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Giới thiệu không được chứa ký tự điều khiển.");
        RuleFor(request => request.Currency)
            .Must(value => value?.Trim().ToUpperInvariant() is "VND" or "USD")
            .WithMessage("Đơn vị tiền tệ chỉ có thể là VND hoặc USD.");
        RuleFor(request => request.HourlyRate)
            .GreaterThanOrEqualTo(0)
            .Must((request, rate) => request.Currency?.Trim().ToUpperInvariant() switch
            {
                "VND" => rate <= 10_000_000m,
                "USD" => rate <= 500m,
                _ => true
            })
            .WithMessage("Mức phí tối đa là 10.000.000 VND hoặc 500 USD.");
        RuleFor(request => request.Timezone).NotEmpty().MaximumLength(100)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Múi giờ không được chứa ký tự điều khiển.");
        RuleFor(request => request.AvatarUrl).MaximumLength(2048)
            .Must(IsOptionalHttpUrl)
            .WithMessage("Avatar phải là URL HTTP/HTTPS hợp lệ.");
    }

    private static bool IsOptionalHttpUrl(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public sealed class UpsertMentorProfileCommandHandler(
    ProfilesDbContext dbContext,
    TimeProvider timeProvider) : ICommandHandler<UpsertMentorProfileCommand>
{
    public async Task<Result> Handle(UpsertMentorProfileCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var profile = await dbContext.MentorProfiles
            .SingleOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);

        if (profile is null)
        {
            var created = MentorProfile.Create(
                request.UserId,
                request.Headline,
                request.Bio,
                request.HourlyRate,
                request.Currency,
                request.Timezone,
                request.IsAcceptingMentees,
                request.AvatarUrl,
                now);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            dbContext.MentorProfiles.Add(created.Value);
        }
        else
        {
            var updated = profile.Update(
                request.Headline,
                request.Bio,
                request.HourlyRate,
                request.Currency,
                request.Timezone,
                request.IsAcceptingMentees,
                request.AvatarUrl,
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
                "Profiles.MentorProfileAlreadyExists",
                "Hồ sơ Mentor vừa được tạo bởi một yêu cầu khác. Vui lòng gửi lại cập nhật."));
        }

        return Result.Success();
    }
}
