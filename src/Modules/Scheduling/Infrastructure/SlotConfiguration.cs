using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Modules.Scheduling.Domain;

namespace SkillBridge.Modules.Scheduling.Infrastructure;

internal sealed class SlotConfiguration : IEntityTypeConfiguration<AvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
    {
        builder.ToTable("availability_slots", table =>
        {
            table.HasCheckConstraint("ck_slots_window", "end_at_utc > start_at_utc");
            table.HasCheckConstraint("ck_slots_status", "status IN ('Available','Locked','Unavailable','Held','Booked')");
            table.HasCheckConstraint("ck_slots_booking", "(status = 'Held' AND booking_id IS NOT NULL AND held_until_utc IS NOT NULL) OR (status = 'Booked' AND booking_id IS NOT NULL AND held_until_utc IS NULL) OR (status IN ('Available','Locked','Unavailable') AND booking_id IS NULL AND held_until_utc IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.HasIndex(x => new { x.MentorId, x.StartAtUtc, x.EndAtUtc }).IsUnique();
        builder.HasIndex(x => x.BookingId);
    }
}

internal sealed class TimeOffConfiguration : IEntityTypeConfiguration<TimeOff>
{
    public void Configure(EntityTypeBuilder<TimeOff> builder)
    {
        builder.ToTable("time_offs", table => table.HasCheckConstraint("ck_time_off_dates", "end_date >= start_date"));
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.MentorId, x.StartDate, x.EndDate });
    }
}
