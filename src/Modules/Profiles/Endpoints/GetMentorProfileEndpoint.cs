using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Profiles.Application.Mentors;

namespace SkillBridge.Modules.Profiles.Endpoints;

public static class GetMentorProfileEndpoint
{
    public static void MapGetMentorProfile(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profiles/mentors")
            .WithTags("Profiles")
            .RequireAuthorization(policy => policy.RequireRole("Mentor"));

        group.MapGet("/me", async (
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetMentorProfileQuery(currentUser.UserId.Value), cancellationToken);
            return result.ToHttpResult();
        }).WithName("GetCurrentMentorProfile");
    }
}
