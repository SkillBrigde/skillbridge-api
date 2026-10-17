using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Scheduling.Application;

namespace SkillBridge.Modules.Scheduling.Endpoints;

public static class RemoveAvailabilityRuleEndpoint
{
    public static void MapRemoveAvailabilityRule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/availability-rules/{ruleId:guid}", async (
            Guid ruleId,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new RemoveAvailabilityRuleCommand(
                currentUser.UserId.Value, ruleId), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).WithName("RemoveCurrentMentorAvailabilityRule");
    }
}
