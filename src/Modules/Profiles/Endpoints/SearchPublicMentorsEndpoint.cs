using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.Mentors;

namespace SkillBridge.Modules.Profiles.Endpoints;

public static class SearchPublicMentorsEndpoint
{
    public static void MapSearchPublicMentors(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/profiles/mentors")
            .WithTags("Profiles")
            .MapGet("/", async (
                string? searchTerm,
                Guid? skillId,
                decimal? minRate,
                decimal? maxRate,
                string? verificationStatus,
                decimal? minRating,
                string? sortBy,
                bool? sortDesc,
                int? page,
                int? pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
                (await sender.Send(new SearchPublicMentorsQuery(
                    searchTerm,
                    skillId,
                    minRate,
                    maxRate,
                    verificationStatus,
                    minRating,
                    sortBy?.Trim() ?? "rating",
                    sortDesc ?? true,
                    page ?? 1,
                    pageSize ?? 10), cancellationToken)).ToHttpResult())
            .AllowAnonymous()
            .WithName("SearchPublicMentors");
    }
}
