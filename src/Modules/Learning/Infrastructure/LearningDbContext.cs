using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.Modules.Learning.Domain;

namespace SkillBridge.Modules.Learning.Infrastructure;

public sealed class LearningDbContext(DbContextOptions<LearningDbContext> options) : DbContext(options)
{
    public const string Schema = "learning";
    public DbSet<LearningSession> Sessions => Set<LearningSession>();
    public DbSet<SessionAttendance> Attendances => Set<SessionAttendance>();
    public DbSet<SessionNote> Notes => Set<SessionNote>();
    public DbSet<SessionMaterial> Materials => Set<SessionMaterial>();
    public DbSet<SessionActionItem> ActionItems => Set<SessionActionItem>();
    public DbSet<LearningReceipt> Inbox => Set<LearningReceipt>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LearningDbContext).Assembly);
        modelBuilder.Entity<LearningReceipt>(b => { b.ToTable("inbox_messages"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); });
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages"); b.HasKey(x => x.Id); b.Property(x => x.Type).HasMaxLength(100);
            b.Property(x => x.Content).HasColumnType("jsonb"); b.HasIndex(x => x.OccurredOnUtc).HasFilter("processed_on_utc IS NULL");
        });
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties()) property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}

public sealed class LearningReceipt
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public DateTimeOffset ProcessedAtUtc { get; set; }
}

internal sealed class LearningSessionConfiguration : IEntityTypeConfiguration<LearningSession>
{
    public void Configure(EntityTypeBuilder<LearningSession> b)
    {
        b.ToTable("learning_sessions", t =>
        {
            t.HasCheckConstraint("ck_learning_window", "scheduled_end_utc > scheduled_start_utc");
            t.HasCheckConstraint("ck_learning_status", "status IN ('Scheduled','InProgress','Completed','Cancelled')");
            t.HasCheckConstraint("ck_learning_completion", "status <> 'Completed' OR (actual_start_utc IS NOT NULL AND actual_end_utc IS NOT NULL AND actual_end_utc >= scheduled_end_utc AND actual_end_utc > actual_start_utc)");
        });
        b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.RoomUrl).HasMaxLength(500); b.Property(x => x.Status).HasMaxLength(20);
        b.HasIndex(x => x.BookingSessionId).IsUnique(); b.HasIndex(x => x.BookingId);
        b.HasIndex(x => new { x.MentorUserId, x.ScheduledStartUtc }); b.HasIndex(x => new { x.MenteeUserId, x.ScheduledStartUtc });
    }
}
internal sealed class AttendanceConfiguration : IEntityTypeConfiguration<SessionAttendance>
{
    public void Configure(EntityTypeBuilder<SessionAttendance> b)
    {
        b.ToTable("session_attendances", t => t.HasCheckConstraint("ck_attendance_window", "left_at_utc IS NULL OR left_at_utc >= joined_at_utc"));
        b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasOne<LearningSession>().WithMany().HasForeignKey(x => x.SessionId);
        b.HasIndex(x => new { x.SessionId, x.UserId }).IsUnique().HasFilter("left_at_utc IS NULL");
    }
}
internal sealed class NoteConfiguration : IEntityTypeConfiguration<SessionNote>
{
    public void Configure(EntityTypeBuilder<SessionNote> b)
    {
        b.ToTable("session_notes"); b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Content).HasMaxLength(10000); b.HasOne<LearningSession>().WithMany().HasForeignKey(x => x.SessionId);
        b.HasIndex(x => new { x.SessionId, x.CreatedAtUtc });
    }
}
internal sealed class MaterialConfiguration : IEntityTypeConfiguration<SessionMaterial>
{
    public void Configure(EntityTypeBuilder<SessionMaterial> b)
    {
        b.ToTable("session_materials", t => t.HasCheckConstraint("ck_material_size", "file_size_bytes BETWEEN 1 AND 10485760 AND octet_length(data) = file_size_bytes"));
        b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.FileName).HasMaxLength(150); b.Property(x => x.ContentType).HasMaxLength(100); b.Property(x => x.Sha256).HasMaxLength(64);
        b.HasOne<LearningSession>().WithMany().HasForeignKey(x => x.SessionId); b.HasIndex(x => new { x.SessionId, x.CreatedAtUtc });
    }
}
internal sealed class ActionItemConfiguration : IEntityTypeConfiguration<SessionActionItem>
{
    public void Configure(EntityTypeBuilder<SessionActionItem> b)
    {
        b.ToTable("session_action_items"); b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.Description).HasMaxLength(4000);
        b.HasOne<LearningSession>().WithMany().HasForeignKey(x => x.SessionId); b.HasIndex(x => new { x.SessionId, x.CreatedAtUtc });
    }
}
