using FluentValidation;
using SkillBridge.Modules.Messaging.Domain;

namespace SkillBridge.Modules.Messaging.Application;

public interface IMessagingUserDirectory
{
    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> IsSessionValidAsync(Guid userId, string securityStamp, CancellationToken cancellationToken);
}

public interface IMessageNotifier
{
    Task PublishAsync(IReadOnlyCollection<Guid> recipients, string eventName, object?[] arguments, CancellationToken cancellationToken);
}

public sealed record CreateConversationRequest(Guid TargetUserId);
public sealed record SendMessageRequest(string? Content, string? Type = "Text");
public sealed record EditMessageRequest(string? Content);
public sealed record MarkReadRequest(Guid MessageId);
public sealed record ConversationDto(Guid Id, Guid ParticipantId, string? LastMessagePreview,
    DateTimeOffset? LastMessageAtUtc, int UnreadCount);
public sealed record ConversationResult(ConversationDto Conversation, bool Created);
public sealed record MessagePage(IReadOnlyList<MessageDto> Items, string? NextCursor);
public sealed record MessageDto(Guid Id, Guid ConversationId, Guid SenderId, long Sequence, string Content,
    string Type, bool IsEdited, bool IsDeleted, DateTimeOffset? EditedAtUtc, DateTimeOffset CreatedAtUtc)
{
    public static MessageDto From(Message message) => new(message.Id, message.ConversationId, message.SenderId,
        message.Sequence, message.Content, message.Type, message.IsEdited, message.IsDeleted, message.EditedAtUtc, message.CreatedAtUtc);
}

public sealed class CreateConversationValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationValidator() => RuleFor(request => request.TargetUserId).NotEmpty();
}

public sealed class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleFor(request => request.Content).NotEmpty().MaximumLength(4000).Must(value => value is null || !value.Contains('\0'));
        RuleFor(request => request.Type).Equal("Text");
    }
}

public sealed class EditMessageValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageValidator() => RuleFor(request => request.Content).NotEmpty().MaximumLength(4000)
        .Must(value => value is null || !value.Contains('\0'));
}

public sealed class MarkReadValidator : AbstractValidator<MarkReadRequest>
{
    public MarkReadValidator() => RuleFor(request => request.MessageId).NotEmpty();
}
