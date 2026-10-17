using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Profiles.Application.Mentors;

namespace SkillBridge.Modules.Profiles.Endpoints;

public static class AddMentorSkillEndpoint
{
    public static void MapAddMentorSkill(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profiles/mentors")
            .WithTags("Profiles")
            .RequireAuthorization(policy => policy.RequireRole("Mentor"));

        group.MapPost("/me/skills", async (
            AddMentorSkillRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new AddMentorSkillCommand(
                currentUser.UserId.Value,
                request.SkillId,
                request.ProficiencyLevel,
                request.YearsOfExperience), cancellationToken);

            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/profiles/mentors/me/skills/{result.Value.SkillId}",
                    result.Value)
                : result.Error.ToProblemDetails();
        }).WithName("AddCurrentMentorSkill");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddMentorSkillRequest(
    Guid SkillId,
    string ProficiencyLevel,
    int YearsOfExperience);
