using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Profiles.Application.Mentors;

namespace SkillBridge.Modules.Profiles.Endpoints;

public static class UpdateMentorVerificationEndpoint
{
    public static void MapUpdateMentorVerification(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profiles/mentors")
            .WithTags("Profiles")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapPut("/{mentorId:guid}/verification", async (
            Guid mentorId,
            UpdateMentorVerificationRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new UpdateMentorVerificationCommand(
                mentorId,
                request.VerificationStatus), cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).WithName("UpdateMentorVerification");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateMentorVerificationRequest(string? VerificationStatus);
