using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Messaging.CQRS;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Application.Users.Responses;
using SkillBridge.Modules.Identity.Infrastructure.Persistence;

namespace SkillBridge.Modules.Identity.Application.Users.GetUsers;

public sealed record GetUsersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    string? Role = null,
    bool? IsActive = null
) : IQuery<PagedResult<UserResponse>>;

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query).Must(query => ((long)query.PageNumber - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("Trang yêu cầu vượt quá giới hạn phân trang.");
        RuleFor(query => query.SearchTerm).MaximumLength(256)
            .Must(term => term is null || !term.Contains('\0'));
        RuleFor(query => query.Role).Must(role => role is null or "Admin" or "Mentor" or "Mentee");
    }
}

public sealed class GetUsersQueryHandler : IQueryHandler<GetUsersQuery, PagedResult<UserResponse>>
{
    private readonly IdentityDbContext _dbContext;

    public GetUsersQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResult<UserResponse>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.ToLower().Contains(term) || u.FullName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = query.Where(u => u.Role == request.Role);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == request.IsActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = request.PageNumber;
        var pageSize = request.PageSize;

        var items = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserResponse(
                u.Id,
                u.Email,
                u.FullName,
                u.Role,
                u.PhoneNumber,
                u.AvatarUrl,
                u.IsEmailConfirmed,
                u.IsActive,
                u.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        var result = PagedResult<UserResponse>.Create(items, page, pageSize, totalCount);

        return Result<PagedResult<UserResponse>>.Success(result);
    }
}
