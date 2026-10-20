using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Scheduling.Application;

namespace SkillBridge.Modules.Scheduling.Endpoints;

public static class GetSchedulingSettingsEndpoint
{
    public static void MapGetSchedulingSettings(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/settings", async (
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(
                new GetSchedulingSettingsQuery(currentUser.UserId.Value),
                cancellationToken);
            return result.ToHttpResult();
        }).WithName("GetCurrentMentorSchedulingSettings");
    }
}
