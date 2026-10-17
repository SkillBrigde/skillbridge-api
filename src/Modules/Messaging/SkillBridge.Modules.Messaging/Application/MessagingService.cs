using System.Globalization;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Messaging.Domain;
using SkillBridge.Modules.Messaging.Infrastructure;

namespace SkillBridge.Modules.Messaging.Application;

public sealed class MessagingService(MessagingDbContext db, IMessagingUserDirectory users,
    IMessageNotifier notifier, ILogger<MessagingService> logger)
{
    public async Task<Result<PagedResult<ConversationDto>>> GetConversationsAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100)
            return Error.Validation("Messaging.Pagination", "Page must be 1–100000 and pageSize 1–100.");
        var query = db.Conversations.AsNoTracking().Where(conversation => conversation.Participant1Id == userId || conversation.Participant2Id == userId);
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(conversation => conversation.LastMessageAtUtc ?? conversation.CreatedAtUtc)
            .ThenByDescending(conversation => conversation.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(conversation => new ConversationDto(conversation.Id,
                conversation.Participant1Id == userId ? conversation.Participant2Id : conversation.Participant1Id,
                conversation.LastMessagePreview, conversation.LastMessageAtUtc,
                db.Messages.Count(message => message.ConversationId == conversation.Id && message.SenderId != userId && !message.IsDeleted &&
                    message.Sequence > db.Participants.Where(participant => participant.ConversationId == conversation.Id && participant.UserId == userId)
                        .Select(participant => participant.LastReadSequence).First()))).ToListAsync(ct);
        return PagedResult<ConversationDto>.Create(items, page, pageSize, count);
    }

    public async Task<Result<ConversationResult>> CreateConversationAsync(Guid userId, CreateConversationRequest request, CancellationToken ct)
    {
        var error = Validate(request, new CreateConversationValidator());
        if (error != Error.None) return error;
        if (userId == request.TargetUserId) return Error.Validation("Messaging.SelfConversation", "Choose another participant.");
        if (!await users.IsActiveAsync(request.TargetUserId, ct))
            return Error.NotFound("Messaging.UserNotFound", "The recipient is unavailable.");
        var conversation = Conversation.Create(userId, request.TargetUserId);
        var existing = await FindPairAsync(conversation, ct);
        if (existing is not null) return new ConversationResult(await ToConversationAsync(existing, userId, ct), false);
        db.Conversations.Add(conversation);
        db.Participants.AddRange(ConversationParticipant.Create(conversation.Id, userId), ConversationParticipant.Create(conversation.Id, request.TargetUserId));
        try
        {
            await db.SaveChangesAsync(ct);
            return new ConversationResult(new(conversation.Id, request.TargetUserId, null, null, 0), true);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            existing = await FindPairAsync(conversation, ct);
            if (existing is null) return Error.Conflict("Messaging.ConversationConflict", "Retry creating the conversation.");
            return new ConversationResult(await ToConversationAsync(existing, userId, ct), false);
        }
    }

    public async Task<Result<MessagePage>> GetMessagesAsync(Guid userId, Guid conversationId, string? cursor, int pageSize, CancellationToken ct)
    {
        if (pageSize is < 1 or > 100) return Error.Validation("Messaging.PageSize", "PageSize must be 1–100.");
        long before = long.MaxValue;
        if (cursor is not null && (!long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out before) || before < 1))
            return Error.Validation("Messaging.Cursor", "Cursor must be a positive message sequence.");
        if (!await IsMemberAsync(userId, conversationId, ct)) return ConversationNotFound();
        var messages = await db.Messages.AsNoTracking().Where(message => message.ConversationId == conversationId && message.Sequence < before)
            .OrderByDescending(message => message.Sequence).Take(pageSize + 1).ToListAsync(ct);
        var more = messages.Count > pageSize;
        var items = messages.Take(pageSize).Select(MessageDto.From).ToArray();
        return new MessagePage(items, more ? items[^1].Sequence.ToString(CultureInfo.InvariantCulture) : null);
    }

    public async Task<Result<MessageDto>> SendAsync(Guid userId, Guid conversationId, SendMessageRequest request, CancellationToken ct)
    {
        var validation = Validate(request, new SendMessageValidator());
        if (validation != Error.None) return validation;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var conversation = await LockConversationAsync(userId, conversationId, ct);
        if (conversation is null) return ConversationNotFound();
        var recipient = conversation.Participant1Id == userId ? conversation.Participant2Id : conversation.Participant1Id;
        if (!await users.IsActiveAsync(recipient, ct)) return Error.Conflict("Messaging.RecipientUnavailable", "The recipient is unavailable.");
        var created = Message.Create(conversationId, userId, conversation.NextSequence(), request.Content, request.Type);
        if (created.IsFailure) return created.Error;
        var message = created.Value;
        db.Messages.Add(message);
        conversation.SetLastMessage(message);
        var sent = new MessageSentIntegrationEvent(Guid.NewGuid(), message.CreatedAtUtc, conversationId, message.Id, userId, recipient);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = sent.EventId,
            Type = nameof(MessageSentIntegrationEvent),
            Content = JsonSerializer.Serialize(sent),
            OccurredOnUtc = sent.OccurredOnUtc
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var dto = MessageDto.From(message);
        await NotifyAsync(conversation, "ReceiveMessage", [dto]);
        return dto;
    }

    public async Task<Result<MessageDto>> EditAsync(Guid userId, Guid conversationId, Guid messageId, EditMessageRequest request, CancellationToken ct)
    {
        var validation = Validate(request, new EditMessageValidator());
        if (validation != Error.None) return validation;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var conversation = await LockConversationAsync(userId, conversationId, ct);
        if (conversation is null) return ConversationNotFound();
        var message = await db.Messages.SingleOrDefaultAsync(message => message.ConversationId == conversationId && message.Id == messageId, ct);
        if (message is null) return MessageNotFound();
        var edited = message.Edit(userId, request.Content);
        if (edited.IsFailure) return edited.Error;
        var last = await db.Messages.Where(item => item.ConversationId == conversationId && !item.IsDeleted).OrderByDescending(item => item.Sequence).FirstOrDefaultAsync(ct);
        conversation.SetLastMessage(last);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await NotifyAsync(conversation, "MessageEdited", [message.Id, message.Content]);
        return MessageDto.From(message);
    }

    public async Task<Result<bool>> DeleteAsync(Guid userId, Guid conversationId, Guid messageId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var conversation = await LockConversationAsync(userId, conversationId, ct);
        if (conversation is null) return ConversationNotFound();
        var message = await db.Messages.SingleOrDefaultAsync(message => message.ConversationId == conversationId && message.Id == messageId, ct);
        if (message is null) return MessageNotFound();
        var deleted = message.Delete(userId);
        if (deleted.IsFailure) return deleted.Error;
        var last = await db.Messages.Where(item => item.ConversationId == conversationId && item.Id != messageId && !item.IsDeleted)
            .OrderByDescending(item => item.Sequence).FirstOrDefaultAsync(ct);
        conversation.SetLastMessage(last);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await NotifyAsync(conversation, "MessageDeleted", [messageId]);
        return true;
    }

    public async Task<Result<bool>> MarkReadAsync(Guid userId, Guid conversationId, MarkReadRequest request, CancellationToken ct)
    {
        var validation = Validate(request, new MarkReadValidator());
        if (validation != Error.None) return validation;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var conversation = await LockConversationAsync(userId, conversationId, ct);
        if (conversation is null) return ConversationNotFound();
        var message = await db.Messages.SingleOrDefaultAsync(message => message.ConversationId == conversationId && message.Id == request.MessageId, ct);
        if (message is null) return MessageNotFound();
        var participant = await db.Participants.SingleAsync(participant => participant.ConversationId == conversationId && participant.UserId == userId, ct);
        participant.Read(message);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct) => await db.Messages.AsNoTracking()
        .CountAsync(message => message.SenderId != userId && !message.IsDeleted && db.Participants.Any(participant =>
            participant.UserId == userId && participant.ConversationId == message.ConversationId && participant.LastReadSequence < message.Sequence), ct);

    public async Task<Result<bool>> TypingAsync(Guid userId, Guid conversationId, bool typing, CancellationToken ct)
    {
        var conversation = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(conversation => conversation.Id == conversationId &&
            (conversation.Participant1Id == userId || conversation.Participant2Id == userId), ct);
        if (conversation is null) return ConversationNotFound();
        var recipient = conversation.Participant1Id == userId ? conversation.Participant2Id : conversation.Participant1Id;
        await notifier.PublishAsync([recipient], typing ? "UserTyping" : "UserStoppedTyping", [conversationId, userId], ct);
        return true;
    }

    private Task<bool> IsMemberAsync(Guid userId, Guid conversationId, CancellationToken ct) => db.Conversations.AsNoTracking().AnyAsync(
        conversation => conversation.Id == conversationId && (conversation.Participant1Id == userId || conversation.Participant2Id == userId), ct);

    private Task<Conversation?> FindPairAsync(Conversation pair, CancellationToken ct) => db.Conversations.AsNoTracking().SingleOrDefaultAsync(
        conversation => conversation.Participant1Id == pair.Participant1Id && conversation.Participant2Id == pair.Participant2Id, ct);

    private Task<Conversation?> LockConversationAsync(Guid userId, Guid conversationId, CancellationToken ct) => db.Conversations.FromSqlInterpolated($"""
        SELECT * FROM messaging.conversations
        WHERE id = {conversationId} AND (participant1_id = {userId} OR participant2_id = {userId}) FOR UPDATE
        """).SingleOrDefaultAsync(ct);

    private async Task<ConversationDto> ToConversationAsync(Conversation conversation, Guid userId, CancellationToken ct)
    {
        var read = await db.Participants.Where(participant => participant.ConversationId == conversation.Id && participant.UserId == userId)
            .Select(participant => participant.LastReadSequence).SingleAsync(ct);
        var unread = await db.Messages.CountAsync(message => message.ConversationId == conversation.Id && message.SenderId != userId &&
            !message.IsDeleted && message.Sequence > read, ct);
        return new(conversation.Id, conversation.Participant1Id == userId ? conversation.Participant2Id : conversation.Participant1Id,
            conversation.LastMessagePreview, conversation.LastMessageAtUtc, unread);
    }

    private async Task NotifyAsync(Conversation conversation, string method, object?[] arguments)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await notifier.PublishAsync([conversation.Participant1Id, conversation.Participant2Id], method, arguments, timeout.Token);
        }
        catch (Exception exception)
        {
            // REST history remains authoritative when a realtime transport is unavailable after commit.
            logger.LogWarning(exception, "Messaging realtime notification failed for conversation {ConversationId}", conversation.Id);
        }
    }

    private static Error Validate<T>(T request, IValidator<T> validator)
    {
        var result = validator.Validate(request);
        return result.IsValid ? Error.None : new ValidationError(result.Errors.GroupBy(failure => failure.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray()));
    }

    private static Error ConversationNotFound() => Error.NotFound("Messaging.ConversationNotFound", "Conversation not found.");
    private static Error MessageNotFound() => Error.NotFound("Messaging.MessageNotFound", "Message not found.");
}

public sealed record MessageSentIntegrationEvent(Guid EventId, DateTimeOffset OccurredOnUtc, Guid ConversationId,
    Guid MessageId, Guid SenderId, Guid RecipientId) : IIntegrationEvent;
