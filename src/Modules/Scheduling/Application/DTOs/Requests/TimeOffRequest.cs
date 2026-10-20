using System.Text.Json.Serialization;
using FluentValidation;

namespace SkillBridge.Modules.Scheduling.Application.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TimeOffRequest(DateOnly? StartDate, DateOnly? EndDate, string? Reason);

public sealed class TimeOffRequestValidator : AbstractValidator<TimeOffRequest>
{
    public TimeOffRequestValidator()
    {
        RuleFor(x => x.StartDate).NotNull();
        RuleFor(x => x.EndDate).NotNull();
        RuleFor(x => x).Must(x => x.StartDate.HasValue && x.EndDate.HasValue && x.StartDate <= x.EndDate &&
            x.EndDate.Value.DayNumber - x.StartDate.Value.DayNumber <= 365 && x.StartDate.Value.Year >= 2000 && x.EndDate.Value.Year <= 9998)
            .WithMessage("Khoảng ngày nghỉ phải hợp lệ và không vượt 366 ngày.");
        RuleFor(x => x.Reason).MaximumLength(1000).Must(x => x is null || !x.Any(char.IsControl));
    }
}
