using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SkillBridge.Modules.Recommendations.Domain;

namespace SkillBridge.Modules.Recommendations.Infrastructure;

public sealed class RecommendationsDbContext(DbContextOptions<RecommendationsDbContext> options) : DbContext(options)
{
    public const string Schema = "recommendations";
    public DbSet<MentorMetric> Metrics => Set<MentorMetric>();
    public DbSet<MenteeInteraction> Interactions => Set<MenteeInteraction>();
    public DbSet<RecommendationLog> Logs => Set<RecommendationLog>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);
        model.Entity<MentorMetric>(b =>
        {
            b.ToTable("mentor_metrics"); b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Prices).HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<ServicePrice[]>(x, (JsonSerializerOptions?)null)!).HasColumnType("jsonb")
                .Metadata.SetValueComparer(new ValueComparer<ServicePrice[]>((a, c) => a!.SequenceEqual(c!),
                    a => a.Aggregate(0, (hash, value) => HashCode.Combine(hash, value)), a => a.ToArray()));
            b.HasIndex(x => x.MentorUserId);
        });
        model.Entity<MenteeInteraction>(b =>
        {
            b.ToTable("interactions", t => t.HasCheckConstraint("ck_interaction_type", "interaction_type IN ('Click','ViewProfile')"));
            b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.InteractionType).HasMaxLength(20); b.Property(x => x.DeduplicationKey).HasMaxLength(64);
            b.HasIndex(x => x.DeduplicationKey).IsUnique(); b.HasIndex(x => new { x.UserId, x.CreatedAtUtc }); b.HasIndex(x => new { x.SkillId, x.CreatedAtUtc });
        });
        model.Entity<RecommendationLog>(b =>
        {
            b.ToTable("recommendation_logs"); b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.AlgorithmVersion).HasMaxLength(30); b.HasIndex(x => x.CreatedAtUtc);
        });
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties()) property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}
