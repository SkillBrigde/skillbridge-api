using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.Mentors;

namespace SkillBridge.Modules.Profiles.Endpoints;

public static class GetPublicMentorProfileEndpoint
{
    public static void MapGetPublicMentorProfile(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/profiles/mentors")
            .WithTags("Profiles")
            .MapGet("/{mentorId:guid}", async (
                Guid mentorId,
                ISender sender,
                CancellationToken cancellationToken) =>
                (await sender.Send(new GetPublicMentorProfileQuery(mentorId), cancellationToken)).ToHttpResult())
            .AllowAnonymous()
            .WithName("GetPublicMentorProfile");
    }
}
