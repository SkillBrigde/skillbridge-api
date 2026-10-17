namespace SkillBridge.Modules.Messaging.Application.DTOs.Responses;

public sealed record ConversationResponse(Guid Id, Guid ParticipantId, string? LastMessagePreview,
    DateTimeOffset? LastMessageAtUtc, int UnreadCount);
