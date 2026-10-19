using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Scheduling.Application;

namespace SkillBridge.Modules.Scheduling.Endpoints;

public static class UpdateAvailabilityRuleEndpoint
{
    public static void MapUpdateAvailabilityRule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/availability-rules/{ruleId:guid}", async (
            Guid ruleId,
            UpdateAvailabilityRuleRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new UpdateAvailabilityRuleCommand(
                currentUser.UserId.Value, ruleId, request.DayOfWeek,
                request.StartTime, request.EndTime, request.IsActive), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).WithName("UpdateCurrentMentorAvailabilityRule");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateAvailabilityRuleRequest(
    [property: JsonRequired] int DayOfWeek,
    [property: JsonRequired] TimeOnly StartTime,
    [property: JsonRequired] TimeOnly EndTime,
    [property: JsonRequired] bool IsActive);
