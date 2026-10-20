using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Catalog.Application.DTOs.Requests;
using SkillBridge.Modules.Catalog.Application.DTOs.Responses;
using SkillBridge.Modules.Catalog.Domain;
using SkillBridge.Modules.Catalog.Infrastructure;

namespace SkillBridge.Modules.Catalog.Application;

public sealed class MentorServices(CatalogDbContext db, IMentorDirectory mentors,
    IValidator<MentorServiceRequest> validator, TimeProvider clock)
{
    public async Task<Result<PagedResult<MentorServiceResponse>>> ListAsync(Guid? mentorId, Guid? ownerUserId,
        string? type, bool? active, int page, int pageSize, CancellationToken ct)
    {
        var offset = CatalogPagination.Offset(page, pageSize);
        if (offset.IsFailure) return offset.Error;
        if (type is not null && !MentorServiceRules.Type(type))
            return Error.Validation("Catalog.InvalidServiceType", "Tier dịch vụ không hợp lệ.");
        var query = db.MentorServices.AsNoTracking();
        if (ownerUserId.HasValue) query = query.Where(x => x.MentorUserId == ownerUserId.Value);
        else query = query.Where(x => x.IsActive);
        if (mentorId.HasValue) query = query.Where(x => x.MentorId == mentorId.Value);
        if (active.HasValue) query = query.Where(x => x.IsActive == active.Value);
        if (type is not null) query = query.Where(x => x.ServiceType == type);
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip(offset.Value).Take(pageSize).ToListAsync(ct);
        return PagedResult<MentorServiceResponse>.Create(items.Select(MentorServiceResponse.From).ToList(), page, pageSize, count);
    }

    public async Task<Result<MentorServiceResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var service = await db.MentorServices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        return service is null ? MentorServiceErrors.Missing : MentorServiceResponse.From(service);
    }

    // Only trusted composition adapters use this DTO; the public API never returns the owner user ID.
    public async Task<Result<MentorServiceSnapshot>> GetBookingSnapshotAsync(Guid id, CancellationToken ct)
    {
        var service = await db.MentorServices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        return service is null ? MentorServiceErrors.Missing : new MentorServiceSnapshot(service.Id, service.Version,
            service.MentorId, service.MentorUserId, service.Title, service.Description, service.ServiceType,
            service.Deliverables, service.Price, service.Currency, service.DurationMinutes, service.TotalSessions, clock.GetUtcNow());
    }

    public async Task<Result<MentorServiceResponse>> CreateAsync(Guid owner, MentorServiceRequest request, CancellationToken ct)
    {
        var validation = await ValidateAsync(request, ct);
        if (validation != Error.None) return validation;
        var mentor = await mentors.GetMentorIdAsync(owner, ct);
        if (mentor.IsFailure) return mentor.Error;
        var created = MentorService.Create(mentor.Value, owner, request.Title, request.Description, request.ServiceType,
            request.DurationMinutes!.Value, request.Price!.Value, request.Currency, request.TotalSessions!.Value,
            request.Deliverables, request.IsActive!.Value, clock.GetUtcNow());
        if (created.IsFailure) return created.Error;
        db.MentorServices.Add(created.Value);
        await db.SaveChangesAsync(ct);
        return MentorServiceResponse.From(created.Value);
    }

    public async Task<Result<MentorServiceResponse>> UpdateAsync(Guid owner, Guid id, long version, MentorServiceRequest request, CancellationToken ct)
    {
        var service = await db.MentorServices.SingleOrDefaultAsync(x => x.Id == id && x.MentorUserId == owner, ct);
        if (service is null) return MentorServiceErrors.Missing;
        if (service.Version != version) return MentorServiceErrors.Version;
        var validation = await ValidateAsync(request, ct);
        if (validation != Error.None) return validation;
        var updated = service.Update(request.Title, request.Description, request.ServiceType, request.DurationMinutes!.Value,
            request.Price!.Value, request.Currency, request.TotalSessions!.Value, request.Deliverables, request.IsActive!.Value, clock.GetUtcNow());
        if (updated.IsFailure) return updated.Error;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return MentorServiceErrors.Version; }
        return MentorServiceResponse.From(service);
    }

    public async Task<Result> DeleteAsync(Guid owner, Guid id, long version, CancellationToken ct)
    {
        var service = await db.MentorServices.SingleOrDefaultAsync(x => x.Id == id && x.MentorUserId == owner, ct);
        if (service is null) return Result.Failure(MentorServiceErrors.Missing);
        if (service.Version != version) return Result.Failure(MentorServiceErrors.Version);
        service.Delete(clock.GetUtcNow());
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(MentorServiceErrors.Version); }
        return Result.Success();
    }

    private async Task<Error> ValidateAsync(MentorServiceRequest request, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(request, ct);
        return result.IsValid ? Error.None : new ValidationError(result.Errors.GroupBy(x => x.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).Distinct().ToArray()));
    }
}
