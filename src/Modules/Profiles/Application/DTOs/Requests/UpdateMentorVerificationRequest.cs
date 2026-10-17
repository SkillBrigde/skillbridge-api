using System.Text.Json.Serialization;

namespace SkillBridge.Modules.Profiles.Application.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateMentorVerificationRequest(string? VerificationStatus);
