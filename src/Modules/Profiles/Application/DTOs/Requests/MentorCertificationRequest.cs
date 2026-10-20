using System.Text.Json.Serialization;

namespace SkillBridge.Modules.Profiles.Application.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MentorCertificationRequest(
    string Name,
    string IssuingOrganization,
    DateTimeOffset IssueDate,
    DateTimeOffset? ExpiryDate,
    string? CredentialUrl,
    string? CredentialId);
