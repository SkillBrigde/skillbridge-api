using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Profiles.Application.Mentors;

namespace SkillBridge.Modules.Profiles.Endpoints;

public static class UpdateMentorProfileEndpoint
{
    public static void MapUpdateMentorProfile(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profiles/mentors")
            .WithTags("Profiles")
            .RequireAuthorization(policy => policy.RequireRole("Mentor"));

        group.MapPut("/me", async (
            UpdateMentorProfileRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new UpsertMentorProfileCommand(
                currentUser.UserId.Value,
                request.Headline,
                request.Bio,
                request.HourlyRate,
                request.Currency,
                request.Timezone,
                request.IsAcceptingMentees,
                request.AvatarUrl), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).WithName("UpdateCurrentMentorProfile");
    }
}

public sealed record UpdateMentorProfileRequest(
    string Headline,
    string Bio,
    decimal HourlyRate,
    string Currency,
    string Timezone,
    bool IsAcceptingMentees,
    string? AvatarUrl = null);
