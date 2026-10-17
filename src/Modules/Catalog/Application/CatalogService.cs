using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Catalog.Domain;
using SkillBridge.Modules.Catalog.Infrastructure;

namespace SkillBridge.Modules.Catalog.Application;

public sealed class CatalogService(CatalogDbContext db)
{
    public async Task<PagedResult<CategoryTreeDto>> GetCategoriesAsync(bool isTree, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking().OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name).ThenBy(category => category.Id).ToListAsync(cancellationToken);
        var items = categories.Select(CategoryTreeDto.From).ToList();
        if (isTree)
        {
            var nodes = items.ToDictionary(category => category.Id);
            var roots = new List<CategoryTreeDto>();
            foreach (var category in items)
            {
                if (category.ParentId is Guid parentId && nodes.TryGetValue(parentId, out var parent))
                    parent.Children.Add(category);
                else
                    roots.Add(category);
            }
            items = roots;
        }
        return PagedResult<CategoryTreeDto>.Create(items, 1, Math.Max(100, items.Count), items.Count);
    }

    public async Task<Result<CategoryDetailsDto>> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await db.Categories.AsNoTracking().SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
        if (category is null) return CategoryNotFound();
        var skills = await db.Skills.AsNoTracking().Where(skill => skill.CategoryId == categoryId)
            .OrderBy(skill => skill.Name).ThenBy(skill => skill.Id)
            .Select(skill => new NamedItemDto(skill.Id, skill.Name, skill.Slug)).ToListAsync(cancellationToken);
        return new CategoryDetailsDto(category.Id, category.Name, category.Slug, category.Description, category.ParentId,
            category.IconUrl, category.SortOrder, category.IsActive, category.CreatedAtUtc, skills);
    }

    public Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken) =>
        WriteAsync<CategoryDto>(async () =>
        {
            var created = Category.Create(request.Name, request.Description, request.ParentId, request.IconUrl, request.SortOrder);
            if (created.IsFailure) return created.Error;
            var category = created.Value;
            var parentError = await ValidateParentAsync(category.Id, category.ParentId, cancellationToken);
            if (parentError != Error.None) return parentError;
            if (await db.Categories.IgnoreQueryFilters().AnyAsync(existing => existing.Slug == category.Slug, cancellationToken))
                return DuplicateSlug();
            db.Categories.Add(category);
            return CategoryDto.From(category);
        }, cancellationToken);

    public Task<Result<bool>> UpdateCategoryAsync(Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken) =>
        WriteAsync<bool>(async () =>
        {
            var category = await db.Categories.SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
            if (category is null) return CategoryNotFound();
            var parentError = await ValidateParentAsync(category.Id, request.ParentId, cancellationToken);
            if (parentError != Error.None) return parentError;
            var updated = category.Update(request.Name, request.Description, request.ParentId, request.IconUrl, request.SortOrder, request.IsActive);
            if (updated.IsFailure) return updated.Error;
            if (await db.Categories.IgnoreQueryFilters().AnyAsync(existing => existing.Id != categoryId && existing.Slug == category.Slug, cancellationToken))
                return DuplicateSlug();
            return true;
        }, cancellationToken);

    public Task<Result<bool>> DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken) =>
        WriteAsync<bool>(async () =>
        {
            var category = await db.Categories.SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
            if (category is null) return CategoryNotFound();
            if (await db.Categories.AnyAsync(child => child.ParentId == categoryId, cancellationToken) ||
                await db.Skills.AnyAsync(skill => skill.CategoryId == categoryId, cancellationToken))
                return Error.Conflict("Catalog.CategoryNotEmpty", "Hãy chuyển hoặc xóa các danh mục con và chuyển kỹ năng trước khi xóa danh mục.");
            category.Delete();
            return true;
        }, cancellationToken);

    public async Task<Result<PagedResult<SkillDto>>> GetSkillsAsync(Guid? categoryId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var offset = CatalogPagination.Offset(page, pageSize);
        if (offset.IsFailure) return offset.Error;
        if (categoryId == Guid.Empty)
            return Error.Validation("Catalog.InvalidCategory", "Category ID không hợp lệ.");
        if (search is { Length: > 150 } || search?.Contains('\0') == true)
            return Error.Validation("Catalog.InvalidSearch", "Từ khóa tối đa 150 ký tự và không chứa ký tự null.");

        var query = db.Skills.AsNoTracking().AsQueryable();
        if (categoryId.HasValue) query = query.Where(skill => skill.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(skill => EF.Functions.ILike(skill.Name, pattern, "\\") ||
                (skill.Description != null && EF.Functions.ILike(skill.Description, pattern, "\\")));
        }
        var total = await query.LongCountAsync(cancellationToken);
        var skills = await query.OrderBy(skill => skill.Name).ThenBy(skill => skill.Id)
            .Skip(offset.Value).Take(pageSize).ToListAsync(cancellationToken);
        return PagedResult<SkillDto>.Create(skills.Select(SkillDto.From).ToList(), page, pageSize, total);
    }

    public async Task<Result<SkillDetailsDto>> GetSkillAsync(Guid skillId, CancellationToken cancellationToken)
    {
        var skill = await db.Skills.AsNoTracking().Include(skill => skill.Tags)
            .SingleOrDefaultAsync(skill => skill.Id == skillId, cancellationToken);
        if (skill is null) return SkillNotFound();
        return new SkillDetailsDto(skill.Id, skill.CategoryId, skill.Name, skill.Slug, skill.Description,
            skill.IsActive, skill.CreatedAtUtc, skill.Tags.OrderBy(tag => tag.Name).ThenBy(tag => tag.Id)
                .Select(tag => new NamedItemDto(tag.Id, tag.Name, tag.Slug)).ToList());
    }

    public Task<Result<SkillDto>> CreateSkillAsync(CreateSkillRequest request, CancellationToken cancellationToken) =>
        WriteAsync<SkillDto>(async () =>
        {
            var tags = await ValidateSkillReferencesAsync(request.CategoryId, request.TagIds, cancellationToken);
            if (tags.IsFailure) return tags.Error;
            var created = Skill.Create(request.CategoryId, request.Name, request.Description, tags.Value);
            if (created.IsFailure) return created.Error;
            var skill = created.Value;
            if (await db.Skills.AnyAsync(existing => existing.Slug == skill.Slug, cancellationToken)) return DuplicateSlug();
            db.Skills.Add(skill);
            var integrationEvent = new SkillCreatedIntegrationEvent(Guid.CreateVersion7(), skill.CreatedAtUtc,
                skill.Id, skill.CategoryId, skill.Name, skill.Slug);
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = integrationEvent.EventId,
                Type = nameof(SkillCreatedIntegrationEvent),
                Content = JsonSerializer.Serialize(integrationEvent),
                OccurredOnUtc = integrationEvent.OccurredOnUtc
            });
            return SkillDto.From(skill);
        }, cancellationToken);

    public Task<Result<bool>> UpdateSkillAsync(Guid skillId, UpdateSkillRequest request, CancellationToken cancellationToken) =>
        WriteAsync<bool>(async () =>
        {
            var skill = await db.Skills.Include(skill => skill.Tags).SingleOrDefaultAsync(skill => skill.Id == skillId, cancellationToken);
            if (skill is null) return SkillNotFound();
            var tags = await ValidateSkillReferencesAsync(request.CategoryId, request.TagIds, cancellationToken);
            if (tags.IsFailure) return tags.Error;
            var updated = skill.Update(request.CategoryId, request.Name, request.Description, request.IsActive, tags.Value);
            if (updated.IsFailure) return updated.Error;
            if (await db.Skills.AnyAsync(existing => existing.Id != skillId && existing.Slug == skill.Slug, cancellationToken))
                return DuplicateSlug();
            return true;
        }, cancellationToken);

    public async Task<PagedResult<NamedItemDto>> GetTagsAsync(CancellationToken cancellationToken)
    {
        var tags = await db.Tags.AsNoTracking().OrderBy(tag => tag.Name).ThenBy(tag => tag.Id)
            .Select(tag => new NamedItemDto(tag.Id, tag.Name, tag.Slug)).ToListAsync(cancellationToken);
        return PagedResult<NamedItemDto>.Create(tags, 1, Math.Max(50, tags.Count), tags.Count);
    }

    public Task<Result<NamedItemDto>> CreateTagAsync(CreateTagRequest request, CancellationToken cancellationToken) =>
        WriteAsync<NamedItemDto>(async () =>
        {
            var created = Tag.Create(request.Name);
            if (created.IsFailure) return created.Error;
            var tag = created.Value;
            if (await db.Tags.AnyAsync(existing => existing.Slug == tag.Slug, cancellationToken)) return DuplicateSlug();
            db.Tags.Add(tag);
            return new NamedItemDto(tag.Id, tag.Name, tag.Slug);
        }, cancellationToken);

    private async Task<Error> ValidateParentAsync(Guid categoryId, Guid? parentId, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking()
            .Select(category => new { category.Id, category.ParentId }).ToListAsync(cancellationToken);
        var parents = categories.ToDictionary(category => category.Id, category => category.ParentId);
        var visited = new HashSet<Guid> { categoryId };
        var depth = 1;
        while (parentId.HasValue)
        {
            if (!visited.Add(parentId.Value))
                return Error.Conflict("Catalog.CategoryCycle", "Danh mục cha không được tạo vòng lặp.");
            if (!parents.TryGetValue(parentId.Value, out var ancestorId))
                return Error.Validation("Catalog.InvalidParent", "Danh mục cha không tồn tại hoặc đã bị xóa.");
            parentId = ancestorId;
            depth++;
        }

        // Count the moved subtree too: parent edits must remain serializable within JSON's default depth limit.
        var children = categories.Where(category => category.ParentId.HasValue).ToLookup(category => category.ParentId!.Value);
        var pending = new Queue<(Guid Id, int Depth)>();
        pending.Enqueue((categoryId, depth));
        while (pending.TryDequeue(out var current))
        {
            if (current.Depth > 16)
                return Error.Validation("Catalog.CategoryTooDeep", "Cây danh mục có tối đa 16 cấp.");
            foreach (var child in children[current.Id])
            {
                if (!visited.Add(child.Id))
                    return Error.Conflict("Catalog.CategoryCycle", "Danh mục cha không được tạo vòng lặp.");
                pending.Enqueue((child.Id, current.Depth + 1));
            }
        }

        return Error.None;
    }

    private async Task<Result<IReadOnlyCollection<Tag>>> ValidateSkillReferencesAsync(Guid categoryId, Guid[]? tagIds, CancellationToken cancellationToken)
    {
        if (!await db.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken))
            return Error.Validation("Catalog.InvalidCategory", "Danh mục không tồn tại hoặc đã bị xóa.");
        if (tagIds is { Length: > 100 } || tagIds?.Contains(Guid.Empty) == true)
            return Error.Validation("Catalog.InvalidTags", "Tối đa 100 tag IDs hợp lệ.");
        var ids = (tagIds ?? []).Distinct().ToArray();
        var tags = await db.Tags.Where(tag => ids.Contains(tag.Id)).ToListAsync(cancellationToken);
        return tags.Count != ids.Length
            ? Error.Validation("Catalog.InvalidTags", "Một hoặc nhiều tag không tồn tại.")
            : Result.Success<IReadOnlyCollection<Tag>>(tags);
    }

    private async Task<Result<T>> WriteAsync<T>(Func<Task<Result<T>>> change, CancellationToken cancellationToken)
    {
        try
        {
            // Serializable also protects parent-cycle and nonempty-category checks against concurrent admin writes.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var result = await change();
            if (result.IsFailure)
            {
                db.ChangeTracker.Clear();
                return result;
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception.GetBaseException() is PostgresException postgres && IsWriteConflict(postgres))
        {
            db.ChangeTracker.Clear();
            return WriteError((PostgresException)exception.GetBaseException());
        }
    }

    private static bool IsWriteConflict(PostgresException exception) => exception.SqlState is
        PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation or
        PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;

    private static Error WriteError(PostgresException exception) => exception.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => DuplicateSlug(),
        PostgresErrorCodes.ForeignKeyViolation => Error.Validation("Catalog.InvalidReference", "Danh mục hoặc tag tham chiếu không còn tồn tại."),
        _ => Error.Conflict("Catalog.ConcurrentChange", "Dữ liệu vừa được thay đổi bởi yêu cầu khác. Vui lòng thử lại.")
    };

    private static Error CategoryNotFound() => Error.NotFound("Catalog.CategoryNotFound", "Category không tồn tại.");
    private static Error SkillNotFound() => Error.NotFound("Catalog.SkillNotFound", "Skill không tồn tại.");
    private static Error DuplicateSlug() => Error.Conflict("Catalog.SlugAlreadyExists", "Tên đã tạo slug trùng với bản ghi hiện có.");
}
