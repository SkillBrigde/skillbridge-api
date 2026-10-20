namespace SkillBridge.Modules.Messaging.Application.DTOs.Responses;

public sealed record MessagePageResponse(IReadOnlyList<MessageResponse> Items, string? NextCursor);
