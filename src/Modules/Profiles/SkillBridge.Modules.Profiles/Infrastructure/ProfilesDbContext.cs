using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Modules.Profiles.Domain;

namespace SkillBridge.Modules.Profiles.Infrastructure;

public sealed class ProfilesDbContext(DbContextOptions<ProfilesDbContext> options) : DbContext(options)
{
    public const string Schema = "profiles";

    public DbSet<MentorProfile> MentorProfiles => Set<MentorProfile>();
    public DbSet<MenteeProfile> MenteeProfiles => Set<MenteeProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProfilesDbContext).Assembly);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
            }
        }
    }
}

internal sealed class MentorProfileConfiguration : IEntityTypeConfiguration<MentorProfile>
{
    public void Configure(EntityTypeBuilder<MentorProfile> builder)
    {
        builder.ToTable("mentor_profiles", table =>
        {
            table.HasCheckConstraint("ck_mentor_profiles_hourly_rate", "hourly_rate >= 0");
            table.HasCheckConstraint("ck_mentor_profiles_currency", "currency IN ('VND', 'USD')");
            table.HasCheckConstraint("ck_mentor_profiles_verification_status", "verification_status IN ('Unverified', 'Pending', 'Verified', 'Rejected')");
        });
        builder.HasKey(profile => profile.Id);
        builder.Ignore(profile => profile.DomainEvents);
        builder.Property(profile => profile.UserId).IsRequired();
        builder.HasIndex(profile => profile.UserId).IsUnique().HasDatabaseName("ux_mentor_profiles_user_id");
        builder.Property(profile => profile.Headline).HasMaxLength(200).IsRequired();
        builder.Property(profile => profile.Bio).HasMaxLength(4000).IsRequired();
        builder.Property(profile => profile.HourlyRate).HasPrecision(18, 2).IsRequired();
        builder.Property(profile => profile.Currency).HasMaxLength(3).IsRequired();
        builder.Property(profile => profile.Timezone).HasMaxLength(100).IsRequired();
        builder.Property(profile => profile.RatingAverage).HasPrecision(3, 2).HasDefaultValue(0m).IsRequired();
        builder.Property(profile => profile.TotalReviews).HasDefaultValue(0).IsRequired();
        builder.Property(profile => profile.TotalSessions).HasDefaultValue(0).IsRequired();
        builder.Property(profile => profile.VerificationStatus).HasMaxLength(30).HasDefaultValue("Unverified").IsRequired();
        builder.Property(profile => profile.IsAcceptingMentees).HasDefaultValue(false).IsRequired();
        builder.Property(profile => profile.AvatarUrl).HasMaxLength(2048);
        builder.Property(profile => profile.CreatedAtUtc).IsRequired();
    }
}

internal sealed class MenteeProfileConfiguration : IEntityTypeConfiguration<MenteeProfile>
{
    public void Configure(EntityTypeBuilder<MenteeProfile> builder)
    {
        builder.ToTable("mentee_profiles", table =>
        {
            table.HasCheckConstraint("ck_mentee_profiles_budget", "budget_min >= 0 AND budget_max >= budget_min");
            table.HasCheckConstraint("ck_mentee_profiles_currency", "currency IN ('VND', 'USD')");
            table.HasCheckConstraint("ck_mentee_profiles_current_level", "current_level IN ('Beginner', 'Intermediate', 'Advanced')");
        });
        builder.HasKey(profile => profile.Id);
        builder.Ignore(profile => profile.DomainEvents);
        builder.Property(profile => profile.UserId).IsRequired();
        builder.HasIndex(profile => profile.UserId).IsUnique().HasDatabaseName("ux_mentee_profiles_user_id");
        builder.Property(profile => profile.CareerGoal).HasMaxLength(2000).IsRequired();
        builder.Property(profile => profile.CurrentLevel).HasMaxLength(30).IsRequired();
        builder.Property(profile => profile.Timezone).HasMaxLength(100).IsRequired();
        builder.Property(profile => profile.BudgetMin).HasPrecision(18, 2).IsRequired();
        builder.Property(profile => profile.BudgetMax).HasPrecision(18, 2).IsRequired();
        builder.Property(profile => profile.Currency).HasMaxLength(3).IsRequired();
        builder.Property(profile => profile.AvatarUrl).HasMaxLength(2048);
        builder.Property(profile => profile.CreatedAtUtc).IsRequired();
    }
}
