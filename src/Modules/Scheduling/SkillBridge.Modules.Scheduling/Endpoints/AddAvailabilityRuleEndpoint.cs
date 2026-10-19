using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Scheduling.Application;

namespace SkillBridge.Modules.Scheduling.Endpoints;

public static class AddAvailabilityRuleEndpoint
{
    public static void MapAddAvailabilityRule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/availability-rules", async (
            AddAvailabilityRuleRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new AddAvailabilityRuleCommand(
                currentUser.UserId.Value, request.DayOfWeek, request.StartTime, request.EndTime), cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/scheduling/availability-rules/{result.Value.Id}", result.Value)
                : result.Error.ToProblemDetails();
        }).WithName("AddCurrentMentorAvailabilityRule");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddAvailabilityRuleRequest(
    [property: JsonRequired] int DayOfWeek,
    [property: JsonRequired] TimeOnly StartTime,
    [property: JsonRequired] TimeOnly EndTime);
