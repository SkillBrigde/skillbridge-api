namespace SkillBridge.Modules.Messaging.Application.DTOs.Requests;

public sealed record SendMessageRequest(string? Content, string? Type = "Text");
