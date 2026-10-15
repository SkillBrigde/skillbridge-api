using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkillBridge.BuildingBlocks.CQRS;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Domain;
using SkillBridge.Modules.Profiles.Infrastructure;

namespace SkillBridge.Modules.Profiles.Application.Mentees;

public sealed record UpsertMenteeProfileCommand(
    Guid UserId,
    string CareerGoal,
    string CurrentLevel,
    string Timezone,
    decimal BudgetMin,
    decimal BudgetMax,
    string Currency,
    string? AvatarUrl) : ICommand;

public sealed class UpsertMenteeProfileCommandValidator : AbstractValidator<UpsertMenteeProfileCommand>
{
    public UpsertMenteeProfileCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CareerGoal).NotEmpty().MaximumLength(2000)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Mục tiêu nghề nghiệp không được chứa ký tự điều khiển.");
        RuleFor(request => request.CurrentLevel)
            .Must(value => value?.Trim() is "Beginner" or "Intermediate" or "Advanced")
            .WithMessage("Trình độ chỉ có thể là Beginner, Intermediate hoặc Advanced.");
        RuleFor(request => request.Timezone).NotEmpty().MaximumLength(100)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("Múi giờ không được chứa ký tự điều khiển.");
        RuleFor(request => request.BudgetMin)
            .InclusiveBetween(0, ProfileInput.MaximumStoredMoney)
            .WithMessage($"budgetMin phải từ 0 đến {ProfileInput.MaximumStoredMoney}.");
        RuleFor(request => request.BudgetMax).GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(ProfileInput.MaximumStoredMoney)
            .WithMessage($"budgetMax không được vượt quá {ProfileInput.MaximumStoredMoney}.")
            .GreaterThanOrEqualTo(request => request.BudgetMin)
            .WithMessage("budgetMax phải lớn hơn hoặc bằng budgetMin.");
        RuleFor(request => request.Currency)
            .Must(value => value?.Trim().ToUpperInvariant() is "VND" or "USD")
            .WithMessage("Đơn vị tiền tệ chỉ có thể là VND hoặc USD.");
        RuleFor(request => request.AvatarUrl).MaximumLength(2048)
            .Must(IsOptionalHttpUrl)
            .WithMessage("Avatar phải là URL HTTP/HTTPS hợp lệ.");
    }

    private static bool IsOptionalHttpUrl(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public sealed class UpsertMenteeProfileCommandHandler(
    ProfilesDbContext dbContext,
    TimeProvider timeProvider) : ICommandHandler<UpsertMenteeProfileCommand>
{
    public async Task<Result> Handle(UpsertMenteeProfileCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var profile = await dbContext.MenteeProfiles
            .SingleOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);

        if (profile is null)
        {
            var created = MenteeProfile.Create(
                request.UserId,
                request.CareerGoal,
                request.CurrentLevel,
                request.Timezone,
                request.BudgetMin,
                request.BudgetMax,
                request.Currency,
                request.AvatarUrl,
                now);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            dbContext.MenteeProfiles.Add(created.Value);
        }
        else
        {
            var updated = profile.Update(
                request.CareerGoal,
                request.CurrentLevel,
                request.Timezone,
                request.BudgetMin,
                request.BudgetMax,
                request.Currency,
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
                "Profiles.MenteeProfileAlreadyExists",
                "Hồ sơ Mentee vừa được tạo bởi một yêu cầu khác. Vui lòng gửi lại cập nhật."));
        }

        return Result.Success();
    }
}
