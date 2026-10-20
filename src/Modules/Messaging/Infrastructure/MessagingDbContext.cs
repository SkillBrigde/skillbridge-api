using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.Modules.Messaging.Domain;

namespace SkillBridge.Modules.Messaging.Infrastructure;

public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options) : DbContext(options)
{
    public const string Schema = "messaging";
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> Participants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MessagingDbContext).Assembly);
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Type).HasMaxLength(500);
            builder.Property(message => message.Content).HasColumnType("jsonb");
            builder.HasIndex(message => message.OccurredOnUtc).HasFilter("processed_on_utc IS NULL");
        });
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations", table => table.HasCheckConstraint("CK_conversations_participants", "participant1_id < participant2_id"));
        builder.HasKey(conversation => conversation.Id);
        builder.Ignore(conversation => conversation.DomainEvents);
        builder.Property(conversation => conversation.LastMessagePreview).HasMaxLength(200);
        builder.HasIndex(conversation => new { conversation.Participant1Id, conversation.Participant2Id }).IsUnique();
        builder.HasIndex(conversation => conversation.Participant2Id);
    }
}

internal sealed class ParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ToTable("message_reads");
        builder.HasKey(participant => participant.Id);
        builder.Ignore(participant => participant.DomainEvents);
        builder.HasIndex(participant => new { participant.ConversationId, participant.UserId }).IsUnique();
        builder.HasOne<Conversation>().WithMany().HasForeignKey(participant => participant.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages", table => table.HasCheckConstraint("CK_messages_sequence", "sequence > 0"));
        builder.HasKey(message => message.Id);
        builder.Ignore(message => message.DomainEvents);
        builder.Property(message => message.Content).HasMaxLength(4000).IsRequired();
        builder.Property(message => message.Type).HasMaxLength(20).IsRequired();
        builder.HasIndex(message => new { message.ConversationId, message.Sequence }).IsUnique();
        builder.HasOne<Conversation>().WithMany().HasForeignKey(message => message.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
