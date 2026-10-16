using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Scheduling.Application;

namespace SkillBridge.Modules.Scheduling.Endpoints;

public static class UpdateSchedulingSettingsEndpoint
{
    public static void MapUpdateSchedulingSettings(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/settings", async (
            UpdateSchedulingSettingsRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new UpdateSchedulingSettingsCommand(
                currentUser.UserId.Value,
                request.SessionDurationMinutes,
                request.BufferTimeMinutes,
                request.LeadTimeHours,
                request.MaxDaysInAdvance,
                request.Timezone), cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).WithName("UpdateCurrentMentorSchedulingSettings");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateSchedulingSettingsRequest(
    int SessionDurationMinutes,
    int BufferTimeMinutes,
    int LeadTimeHours,
    int MaxDaysInAdvance,
    string Timezone);
