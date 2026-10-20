namespace SkillBridge.Modules.Identity.Application.Auth.DTOs.Requests;

public sealed record LoginRequest(
    string Email,
    string Password,
    // Retained for the existing request contract; the endpoint uses the connection IP.
    string? IpAddress = null
);
