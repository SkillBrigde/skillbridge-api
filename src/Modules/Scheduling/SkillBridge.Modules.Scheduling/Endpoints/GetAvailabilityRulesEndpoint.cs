using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Scheduling.Application;

namespace SkillBridge.Modules.Scheduling.Endpoints;

public static class GetAvailabilityRulesEndpoint
{
    public static void MapGetAvailabilityRules(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/availability-rules", async (
            int? page,
            int? pageSize,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetAvailabilityRulesQuery(
                currentUser.UserId.Value, page ?? 1, pageSize ?? 100), cancellationToken);
            return result.ToHttpResult();
        }).WithName("GetCurrentMentorAvailabilityRules");
    }
}
