using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.Modules.Messaging.Domain;

public sealed class Message : AggregateRoot<Guid>
{
    private Message() { }
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public long Sequence { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string Type { get; private set; } = "Text";
    public bool IsEdited { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? EditedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<Message> Create(Guid conversationId, Guid senderId, long sequence, string? content, string? type)
    {
        if (conversationId == Guid.Empty || senderId == Guid.Empty || sequence <= 0)
            return Error.Validation("Messaging.InvalidMessage", "Conversation, sender and sequence must be valid.");
        if (type != "Text") return Error.Validation("Messaging.MessageType", "Only Text messages are supported.");
        var error = ValidateContent(content);
        if (error != Error.None) return error;
        return new Message
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversationId,
            SenderId = senderId,
            Sequence = sequence,
            Content = content!.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public Result Edit(Guid userId, string? content)
    {
        if (SenderId != userId) return Result.Failure(Error.Forbidden("Messaging.NotSender", "Only the sender can edit a message."));
        if (IsDeleted) return Result.Failure(Error.Conflict("Messaging.MessageDeleted", "The message has been deleted."));
        var error = ValidateContent(content);
        if (error != Error.None) return Result.Failure(error);
        Content = content!.Trim();
        IsEdited = true;
        EditedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Delete(Guid userId)
    {
        if (SenderId != userId) return Result.Failure(Error.Forbidden("Messaging.NotSender", "Only the sender can delete a message."));
        Content = string.Empty;
        IsDeleted = true;
        return Result.Success();
    }

    private static Error ValidateContent(string? content) =>
        string.IsNullOrWhiteSpace(content) || content.Length > 4000 || content.Contains('\0')
            ? Error.Validation("Messaging.Content", "Content must contain 1–4000 characters without NUL.") : Error.None;
}
