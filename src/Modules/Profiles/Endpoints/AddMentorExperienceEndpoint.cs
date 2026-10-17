using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Profiles.Application.DTOs.Requests;
using SkillBridge.Modules.Profiles.Application.Mentors;

namespace SkillBridge.Modules.Profiles.Endpoints;

public static class AddMentorExperienceEndpoint
{
    public static void MapAddMentorExperience(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profiles/mentors")
            .WithTags("Profiles")
            .RequireAuthorization(policy => policy.RequireRole("Mentor"));

        group.MapPost("/me/experiences", async (
            MentorExperienceRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new AddMentorExperienceCommand(
                currentUser.UserId.Value,
                request.Company,
                request.Position,
                request.StartDate,
                request.EndDate,
                request.Description,
                request.IsCurrent), cancellationToken);

            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/profiles/mentors/me/experiences/{result.Value.Id}",
                    result.Value)
                : result.Error.ToProblemDetails();
        }).WithName("AddCurrentMentorExperience");
    }
}
