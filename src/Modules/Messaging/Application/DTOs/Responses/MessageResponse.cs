using SkillBridge.Modules.Messaging.Domain;

namespace SkillBridge.Modules.Messaging.Application.DTOs.Responses;

public sealed record MessageResponse(Guid Id, Guid ConversationId, Guid SenderId, long Sequence, string Content,
    string Type, bool IsEdited, bool IsDeleted, DateTimeOffset? EditedAtUtc, DateTimeOffset CreatedAtUtc)
{
    public static MessageResponse From(Message message) => new(message.Id, message.ConversationId, message.SenderId,
        message.Sequence, message.Content, message.Type, message.IsEdited, message.IsDeleted, message.EditedAtUtc, message.CreatedAtUtc);
}
