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

public static class AddMentorCertificationEndpoint
{
    public static void MapAddMentorCertification(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profiles/mentors")
            .WithTags("Profiles")
            .RequireAuthorization(policy => policy.RequireRole("Mentor"));

        group.MapPost("/me/certifications", async (
            MentorCertificationRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new AddMentorCertificationCommand(
                currentUser.UserId.Value,
                request.Name,
                request.IssuingOrganization,
                request.IssueDate,
                request.ExpiryDate,
                request.CredentialUrl,
                request.CredentialId), cancellationToken);

            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/profiles/mentors/me/certifications/{result.Value.Id}",
                    result.Value)
                : result.Error.ToProblemDetails();
        }).WithName("AddCurrentMentorCertification");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MentorCertificationRequest(
    string Name,
    string IssuingOrganization,
    DateTimeOffset IssueDate,
    DateTimeOffset? ExpiryDate,
    string? CredentialUrl,
    string? CredentialId);
