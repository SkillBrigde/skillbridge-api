using SkillBridge.BuildingBlocks.Domain;

namespace SkillBridge.Modules.Messaging.Domain;

public sealed class Conversation : AggregateRoot<Guid>
{
    private Conversation() { }
    public Guid Participant1Id { get; private set; }
    public Guid Participant2Id { get; private set; }
    public long LastSequence { get; private set; }
    public string? LastMessagePreview { get; private set; }
    public DateTimeOffset? LastMessageAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Conversation Create(Guid firstUserId, Guid secondUserId)
    {
        var ordered = string.CompareOrdinal(firstUserId.ToString("N"), secondUserId.ToString("N")) < 0;
        return new Conversation
        {
            Id = Guid.CreateVersion7(),
            Participant1Id = ordered ? firstUserId : secondUserId,
            Participant2Id = ordered ? secondUserId : firstUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public bool Includes(Guid userId) => Participant1Id == userId || Participant2Id == userId;
    public long NextSequence() => ++LastSequence;
    public void SetLastMessage(Message? message)
    {
        LastMessagePreview = message is null ? null : message.Content[..Math.Min(200, message.Content.Length)];
        LastMessageAtUtc = message?.CreatedAtUtc;
    }
}

public sealed class ConversationParticipant : AggregateRoot<Guid>
{
    private ConversationParticipant() { }
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public long LastReadSequence { get; private set; }
    public Guid? LastReadMessageId { get; private set; }
    public DateTimeOffset? LastReadAtUtc { get; private set; }

    public static ConversationParticipant Create(Guid conversationId, Guid userId) => new()
    {
        Id = Guid.CreateVersion7(),
        ConversationId = conversationId,
        UserId = userId
    };

    public void Read(Message message)
    {
        if (message.Sequence <= LastReadSequence) return;
        LastReadSequence = message.Sequence;
        LastReadMessageId = message.Id;
        LastReadAtUtc = DateTimeOffset.UtcNow;
    }
}
