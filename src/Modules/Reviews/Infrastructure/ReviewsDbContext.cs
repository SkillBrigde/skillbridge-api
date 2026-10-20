using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.Modules.Reviews.Domain;

namespace SkillBridge.Modules.Reviews.Infrastructure;

public sealed class ReviewsDbContext(DbContextOptions<ReviewsDbContext> options) : DbContext(options)
{
    public const string Schema = "reviews";
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);
        model.Entity<Review>(b =>
        {
            b.ToTable("reviews", t => t.HasCheckConstraint("ck_review_ratings", "overall_rating BETWEEN 1 AND 5 AND communication_rating BETWEEN 1 AND 5 AND expertise_rating BETWEEN 1 AND 5 AND helpfulness_rating BETWEEN 1 AND 5"));
            b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Comment).HasMaxLength(4000); b.Property(x => x.MentorReply).HasMaxLength(4000);
            b.HasIndex(x => x.BookingId).IsUnique(); b.HasIndex(x => new { x.MentorId, x.IsVisible, x.CreatedAtUtc }); b.HasIndex(x => x.MenteeUserId);
        });
        model.Entity<OutboxMessage>(b => { b.ToTable("outbox_messages"); b.HasKey(x => x.Id); b.Property(x => x.Type).HasMaxLength(100); b.Property(x => x.Content).HasColumnType("jsonb"); });
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties()) property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}
