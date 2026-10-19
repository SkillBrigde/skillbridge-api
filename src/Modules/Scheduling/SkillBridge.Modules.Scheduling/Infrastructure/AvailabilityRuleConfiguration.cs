using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Modules.Scheduling.Domain;

namespace SkillBridge.Modules.Scheduling.Infrastructure;

internal sealed class AvailabilityRuleConfiguration : IEntityTypeConfiguration<AvailabilityRule>
{
    public void Configure(EntityTypeBuilder<AvailabilityRule> builder)
    {
        builder.ToTable("availability_rules", table =>
        {
            table.HasCheckConstraint("ck_availability_rules_mentor_id",
                "mentor_id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("ck_availability_rules_day_of_week", "day_of_week BETWEEN 0 AND 6");
            table.HasCheckConstraint("ck_availability_rules_time_range", "start_time < end_time");
        });
        builder.HasKey(rule => rule.Id);
        builder.Ignore(rule => rule.DomainEvents);
        builder.Property(rule => rule.MentorId).IsRequired();
        builder.Property(rule => rule.DayOfWeek).IsRequired();
        builder.Property(rule => rule.StartTime).HasPrecision(0).IsRequired();
        builder.Property(rule => rule.EndTime).HasPrecision(0).IsRequired();
        builder.Property(rule => rule.IsActive).IsRequired();
        builder.Property(rule => rule.CreatedAtUtc).IsRequired();
        builder.HasIndex(rule => new { rule.MentorId, rule.DayOfWeek, rule.StartTime, rule.Id })
            .HasDatabaseName("ix_availability_rules_mentor_day_start");
    }
}
