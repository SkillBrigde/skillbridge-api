using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Profiles.Application.Mentors;

namespace SkillBridge.Modules.Profiles.Endpoints;

public static class UpdateMentorExperienceEndpoint
{
    public static void MapUpdateMentorExperience(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profiles/mentors")
            .WithTags("Profiles")
            .RequireAuthorization(policy => policy.RequireRole("Mentor"));

        group.MapPut("/me/experiences/{experienceId:guid}", async (
            Guid experienceId,
            MentorExperienceRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new UpdateMentorExperienceCommand(
                currentUser.UserId.Value,
                experienceId,
                request.Company,
                request.Position,
                request.StartDate,
                request.EndDate,
                request.Description,
                request.IsCurrent), cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).WithName("UpdateCurrentMentorExperience");
    }
}
