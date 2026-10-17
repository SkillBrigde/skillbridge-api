using System.Text.Json.Serialization;
using FluentValidation;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Catalog.Domain;

namespace SkillBridge.Modules.Catalog.Application;

public interface IMentorDirectory
{
    Task<Result<Guid>> GetMentorIdAsync(Guid userId, CancellationToken cancellationToken);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MentorServiceRequest(string? Title, string? Description, string? ServiceType,
    int? DurationMinutes, decimal? Price, string? Currency, int? TotalSessions, string? Deliverables, bool? IsActive);

public sealed record MentorServiceDto(Guid Id, Guid MentorId, string Title, string Description, string ServiceType,
    int DurationMinutes, decimal Price, string Currency, int TotalSessions, string Deliverables,
    bool IsActive, long Version, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc)
{
    public static MentorServiceDto From(MentorService service) => new(service.Id, service.MentorId,
        service.Title, service.Description, service.ServiceType, service.DurationMinutes, service.Price,
        service.Currency, service.TotalSessions, service.Deliverables, service.IsActive, service.Version,
        service.CreatedAtUtc, service.UpdatedAtUtc);
}

public sealed record MentorServiceSnapshot(Guid ServiceId, long ServiceVersion, Guid MentorId, Guid MentorUserId,
    string ServiceTitle, string ServiceDescription, string ServiceType, string Deliverables, decimal Price,
    string Currency, int DurationMinutes, int TotalSessions, DateTimeOffset CapturedAtUtc);

public sealed class MentorServiceValidator : AbstractValidator<MentorServiceRequest>
{
    public MentorServiceValidator()
    {
        RuleFor(x => x.Title).Must(x => MentorServiceRules.Text(x, 200));
        RuleFor(x => x.Description).Must(x => MentorServiceRules.Text(x, 4000, true));
        RuleFor(x => x.Deliverables).Must(x => MentorServiceRules.Text(x, 4000, true));
        RuleFor(x => x.ServiceType).Must(MentorServiceRules.Type);
        RuleFor(x => x.DurationMinutes).NotNull().InclusiveBetween(15, 240);
        RuleFor(x => x.TotalSessions).NotNull().InclusiveBetween(1, 52);
        RuleFor(x => x.Currency).Must(x => x?.Trim().ToUpperInvariant() is "VND" or "USD");
        RuleFor(x => x.Price).NotNull().Must((request, price) => price.HasValue &&
            MentorServiceRules.Price(price.Value, request.Currency?.Trim().ToUpperInvariant()));
        RuleFor(x => x.IsActive).NotNull();
    }
}

public static class MentorServiceErrors
{
    public static readonly Error Missing = Error.NotFound("Catalog.ServiceNotFound", "Không tìm thấy dịch vụ.");
    public static readonly Error Version = Error.Conflict("Catalog.ServiceVersionMismatch", "Dịch vụ đã thay đổi; hãy tải lại dữ liệu.");
    public static readonly Error Unavailable = Error.Failure("Catalog.MentorDirectoryUnavailable", "Nguồn hồ sơ Mentor tạm thời chưa khả dụng.");
    public static readonly Error ProfileRequired = Error.Conflict("Catalog.MentorProfileRequired", "Hãy tạo hồ sơ Mentor trước khi tạo dịch vụ.");
}

internal sealed class UnavailableMentorDirectory : IMentorDirectory
{
    public Task<Result<Guid>> GetMentorIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<Guid>(MentorServiceErrors.Unavailable));
}
