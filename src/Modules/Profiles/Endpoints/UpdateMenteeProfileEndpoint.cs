using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Profiles.Application.DTOs.Requests;
using SkillBridge.Modules.Profiles.Application.Mentees;

namespace SkillBridge.Modules.Profiles.Endpoints;

public static class UpdateMenteeProfileEndpoint
{
    public static void MapUpdateMenteeProfile(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profiles/mentees")
            .WithTags("Profiles")
            .RequireAuthorization(policy => policy.RequireRole("Mentee"));

        group.MapPut("/me", async (
            UpdateMenteeProfileRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new UpsertMenteeProfileCommand(
                currentUser.UserId.Value,
                request.CareerGoal,
                request.CurrentLevel,
                request.Timezone,
                request.BudgetMin,
                request.BudgetMax,
                request.Currency,
                request.AvatarUrl), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).WithName("UpdateCurrentMenteeProfile");
    }
}
