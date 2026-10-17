using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Catalog.Application;
using SkillBridge.Modules.Catalog.Application.DTOs.Requests;
using SkillBridge.Modules.Catalog.Application.DTOs.Responses;

namespace SkillBridge.Modules.Catalog.Endpoints;

public static class MentorServiceEndpoints
{
    public static void MapMentorServiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/catalog").WithTags("Mentor services");
        group.MapGet("/mentors/{mentorId:guid}/services", async (Guid mentorId, MentorServices service, CancellationToken ct,
            string? serviceType = null, int page = 1, int pageSize = 10) =>
            (await service.ListAsync(mentorId, null, serviceType, null, page, pageSize, ct)).ToHttpResult());
        group.MapGet("/services/{serviceId:guid}", async (Guid serviceId, MentorServices service, HttpContext context, CancellationToken ct) =>
            Read(await service.GetAsync(serviceId, ct), context));
        group.MapGet("/services/me", async (MentorServices service, ICurrentUser user, CancellationToken ct,
            string? serviceType = null, bool? isActive = null, int page = 1, int pageSize = 10) =>
            user.UserId is not { } owner ? Results.Unauthorized() :
                (await service.ListAsync(null, owner, serviceType, isActive, page, pageSize, ct)).ToHttpResult())
            .RequireAuthorization(policy => policy.RequireRole("Mentor"));
        group.MapPost("/services", async (MentorServiceRequest request, MentorServices service, ICurrentUser user,
            HttpContext context, CancellationToken ct) =>
        {
            if (user.UserId is not { } owner) return Results.Unauthorized();
            var result = await service.CreateAsync(owner, request, ct);
            if (result.IsFailure) return Problem(result.Error);
            context.Response.Headers.ETag = Quote(result.Value.Version);
            return Results.Created($"/api/v1/catalog/services/{result.Value.Id}", result.Value);
        }).RequireAuthorization(policy => policy.RequireRole("Mentor"));
        group.MapPut("/services/{serviceId:guid}", async (Guid serviceId, MentorServiceRequest request,
            MentorServices service, ICurrentUser user, HttpContext context, CancellationToken ct) =>
        {
            var version = Version(context);
            if (user.UserId is not { } owner) return Results.Unauthorized();
            return version.IsFailure ? Problem(version.Error) : Read(
                await service.UpdateAsync(owner, serviceId, version.Value, request, ct), context);
        }).RequireAuthorization(policy => policy.RequireRole("Mentor"));
        group.MapDelete("/services/{serviceId:guid}", async (Guid serviceId, MentorServices service,
            ICurrentUser user, HttpContext context, CancellationToken ct) =>
        {
            var version = Version(context);
            if (version.IsFailure) return Problem(version.Error);
            if (user.UserId is not { } owner) return Results.Unauthorized();
            var result = await service.DeleteAsync(owner, serviceId, version.Value, ct);
            return result.IsFailure ? Problem(result.Error) : Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Mentor"));
    }

    private static IResult Read(Result<MentorServiceResponse> result, HttpContext context)
    {
        if (result.IsFailure) return Problem(result.Error);
        context.Response.Headers.ETag = Quote(result.Value.Version);
        return Results.Ok(result.Value);
    }

    private static string Quote(long version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    private static Result<long> Version(HttpContext context)
    {
        var values = context.Request.Headers.IfMatch;
        if (values.Count == 0) return Error.Validation("Catalog.ServiceVersionRequired", "Cần gửi header If-Match.");
        var value = values.ToString();
        if (values.Count != 1 || value.Length < 3 || value[0] != '"' || value[^1] != '"' ||
            !long.TryParse(value.AsSpan(1, value.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version <= 0)
            return Error.Validation("Catalog.InvalidServiceVersion", "If-Match phải là phiên bản nguyên dương trong dấu ngoặc kép.");
        return version;
    }

    private static IResult Problem(Error error) => error.Code switch
    {
        "Catalog.ServiceVersionRequired" => Results.Problem(statusCode: 428, title: error.Code, detail: error.Description),
        "Catalog.ServiceVersionMismatch" => Results.Problem(statusCode: 412, title: error.Code, detail: error.Description),
        "Catalog.MentorDirectoryUnavailable" => Results.Problem(statusCode: 503, title: error.Code, detail: error.Description),
        _ => error.ToProblemDetails()
    };
}
