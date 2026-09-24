using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Catalog.Application;

namespace SkillBridge.Modules.Catalog.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/catalog").WithTags("Catalog");

        group.MapGet("/categories", async (bool? isTree, CatalogService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCategoriesAsync(isTree ?? false, cancellationToken)))
            .AllowAnonymous().WithName("GetCatalogCategories");

        group.MapGet("/categories/{categoryId:guid}", async (Guid categoryId, CatalogService service, CancellationToken cancellationToken) =>
            (await service.GetCategoryAsync(categoryId, cancellationToken)).ToHttpResult())
            .AllowAnonymous().WithName("GetCatalogCategory");

        group.MapPost("/categories", async (CreateCategoryRequest request, CatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateCategoryAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/catalog/categories/{result.Value.Id}", result.Value)
                : result.Error.ToProblemDetails();
        }).RequireAuthorization(policy => policy.RequireRole("Admin")).WithName("CreateCatalogCategory");

        group.MapPut("/categories/{categoryId:guid}", async (Guid categoryId, UpdateCategoryRequest request, CatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateCategoryAsync(categoryId, request, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).RequireAuthorization(policy => policy.RequireRole("Admin")).WithName("UpdateCatalogCategory");

        group.MapDelete("/categories/{categoryId:guid}", async (Guid categoryId, CatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteCategoryAsync(categoryId, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).RequireAuthorization(policy => policy.RequireRole("Admin")).WithName("DeleteCatalogCategory");

        group.MapGet("/skills", async (Guid? categoryId, string? search, int? page, int? pageSize, CatalogService service, CancellationToken cancellationToken) =>
            (await service.GetSkillsAsync(categoryId, search, page ?? 1, pageSize ?? 10, cancellationToken)).ToHttpResult())
            .AllowAnonymous().WithName("GetCatalogSkills");

        group.MapGet("/skills/{skillId:guid}", async (Guid skillId, CatalogService service, CancellationToken cancellationToken) =>
            (await service.GetSkillAsync(skillId, cancellationToken)).ToHttpResult())
            .AllowAnonymous().WithName("GetCatalogSkill");

        group.MapPost("/skills", async (CreateSkillRequest request, CatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateSkillAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/catalog/skills/{result.Value.Id}", result.Value)
                : result.Error.ToProblemDetails();
        }).RequireAuthorization(policy => policy.RequireRole("Admin")).WithName("CreateCatalogSkill");

        group.MapPut("/skills/{skillId:guid}", async (Guid skillId, UpdateSkillRequest request, CatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateSkillAsync(skillId, request, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).RequireAuthorization(policy => policy.RequireRole("Admin")).WithName("UpdateCatalogSkill");

        group.MapGet("/tags", async (CatalogService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetTagsAsync(cancellationToken)))
            .AllowAnonymous().WithName("GetCatalogTags");

        group.MapPost("/tags", async (CreateTagRequest request, CatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateTagAsync(request, cancellationToken);
            return result.IsSuccess ? Results.Created("/api/v1/catalog/tags", result.Value) : result.Error.ToProblemDetails();
        }).RequireAuthorization(policy => policy.RequireRole("Admin")).WithName("CreateCatalogTag");
    }
}
