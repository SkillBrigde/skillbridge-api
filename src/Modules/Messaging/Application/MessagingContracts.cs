using FluentValidation;
using SkillBridge.Modules.Messaging.Application.DTOs.Requests;
using SkillBridge.Modules.Messaging.Application.DTOs.Responses;
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

public sealed record ConversationResult(ConversationResponse Conversation, bool Created);
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
