using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.Modules.Booking.Domain;

namespace SkillBridge.Modules.Booking.Infrastructure;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public const string Schema = "booking";
    public DbSet<BookingOrder> Bookings => Set<BookingOrder>();
    public DbSet<BookingSession> Sessions => Set<BookingSession>();
    public DbSet<BookingAudit> AuditLogs => Set<BookingAudit>();
    public DbSet<RescheduleRequest> RescheduleRequests => Set<RescheduleRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages"); b.HasKey(x => x.Id);
            b.Property(x => x.Type).HasMaxLength(100); b.Property(x => x.Content).HasColumnType("jsonb");
            b.Property<DateTimeOffset?>("RetryAfterUtc");
            b.HasIndex(x => x.OccurredOnUtc).HasFilter("processed_on_utc IS NULL");
        });
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}

internal sealed class BookingOrderConfiguration : IEntityTypeConfiguration<BookingOrder>
{
    public void Configure(EntityTypeBuilder<BookingOrder> b)
    {
        b.ToTable("bookings", t => t.HasCheckConstraint("ck_booking_status",
            "status IN ('Holding','PendingPayment','AwaitingMentor','Confirmed','Completed','Cancelled','RefundReview')"));
        b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Terms).HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
            x => JsonSerializer.Deserialize<ServiceTerms>(x, (JsonSerializerOptions?)null)!).HasColumnType("jsonb");
        b.Property(x => x.Intake).HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
            x => JsonSerializer.Deserialize<IntakeForm>(x, (JsonSerializerOptions?)null)!).HasColumnType("jsonb");
        b.Property(x => x.IdempotencyKey).HasMaxLength(100); b.Property(x => x.RequestHash).HasMaxLength(64);
        b.Property(x => x.BookingCode).HasMaxLength(50); b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.MentorNote).HasMaxLength(2000); b.Property(x => x.CancellationReason).HasMaxLength(2000);
        b.HasIndex(x => new { x.MenteeUserId, x.IdempotencyKey }).IsUnique(); b.HasIndex(x => x.BookingCode).IsUnique();
        b.HasIndex(x => x.PaymentId).IsUnique(); b.HasIndex(x => new { x.Status, x.HeldUntilUtc });
        b.HasMany(x => x.Sessions).WithOne().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.AuditLogs).WithOne().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.RescheduleRequests).WithOne().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.MentorUserId);
    }
}

internal sealed class RescheduleRequestConfiguration : IEntityTypeConfiguration<RescheduleRequest>
{
    public void Configure(EntityTypeBuilder<RescheduleRequest> b)
    {
        b.ToTable("reschedule_requests", t => t.HasCheckConstraint("ck_reschedule_status", "status IN ('Pending','Applying','Approved','Rejected')"));
        b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.Reason).HasMaxLength(2000); b.Property(x => x.ResponseNote).HasMaxLength(2000);
        b.HasOne<BookingSession>().WithMany().HasForeignKey(x => x.BookingSessionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.BookingSessionId).IsUnique().HasFilter("status IN ('Pending','Applying')");
    }
}

internal sealed class BookingSessionConfiguration : IEntityTypeConfiguration<BookingSession>
{
    public void Configure(EntityTypeBuilder<BookingSession> b)
    {
        b.ToTable("booking_sessions", t => t.HasCheckConstraint("ck_booking_session_window",
            "(start_at_utc IS NULL AND end_at_utc IS NULL) OR (start_at_utc IS NOT NULL AND end_at_utc > start_at_utc)"));
        b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.BookingId, x.SlotId }).IsUnique();
    }
}

internal sealed class BookingAuditConfiguration : IEntityTypeConfiguration<BookingAudit>
{
    public void Configure(EntityTypeBuilder<BookingAudit> b)
    {
        b.ToTable("booking_audit_logs"); b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.OldStatus).HasMaxLength(30); b.Property(x => x.NewStatus).HasMaxLength(30);
        b.Property(x => x.Reason).HasMaxLength(2000); b.HasIndex(x => new { x.BookingId, x.CreatedAtUtc });
    }
}
