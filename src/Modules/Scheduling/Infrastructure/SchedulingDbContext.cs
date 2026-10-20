using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Modules.Scheduling.Domain;

namespace SkillBridge.Modules.Scheduling.Infrastructure;

public sealed class SchedulingDbContext(DbContextOptions<SchedulingDbContext> options) : DbContext(options)
{
    public const string Schema = "scheduling";

    public DbSet<SchedulingSettings> SchedulingSettings => Set<SchedulingSettings>();
    public DbSet<AvailabilityRule> AvailabilityRules => Set<AvailabilityRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulingDbContext).Assembly);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
            }
        }
    }
}

internal sealed class SchedulingSettingsConfiguration : IEntityTypeConfiguration<SchedulingSettings>
{
    public void Configure(EntityTypeBuilder<SchedulingSettings> builder)
    {
        builder.ToTable("scheduling_settings", table =>
        {
            table.HasCheckConstraint(
                "ck_scheduling_settings_mentor_id",
                "mentor_id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint(
                "ck_scheduling_settings_session_duration",
                "session_duration_minutes BETWEEN 15 AND 240 AND session_duration_minutes % 15 = 0");
            table.HasCheckConstraint(
                "ck_scheduling_settings_buffer_time",
                "buffer_time_minutes BETWEEN 0 AND 120 AND buffer_time_minutes % 5 = 0");
            table.HasCheckConstraint(
                "ck_scheduling_settings_lead_time",
                "lead_time_hours BETWEEN 0 AND 720");
            table.HasCheckConstraint(
                "ck_scheduling_settings_advance_window",
                "max_days_in_advance BETWEEN 1 AND 365 AND lead_time_hours <= max_days_in_advance * 24");
        });
        builder.HasKey(settings => settings.Id);
        builder.Ignore(settings => settings.DomainEvents);
        builder.Property(settings => settings.MentorId).IsRequired();
        builder.HasIndex(settings => settings.MentorId)
            .IsUnique()
            .HasDatabaseName("ux_scheduling_settings_mentor_id");
        builder.Property(settings => settings.SessionDurationMinutes).IsRequired();
        builder.Property(settings => settings.BufferTimeMinutes).IsRequired();
        builder.Property(settings => settings.LeadTimeHours).IsRequired();
        builder.Property(settings => settings.MaxDaysInAdvance).IsRequired();
        builder.Property(settings => settings.Timezone).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.CreatedAtUtc).IsRequired();
    }
}
