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
    public DbSet<MentorSkill> MentorSkills => Set<MentorSkill>();

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

internal sealed class MentorSkillConfiguration : IEntityTypeConfiguration<MentorSkill>
{
    public void Configure(EntityTypeBuilder<MentorSkill> builder)
    {
        builder.ToTable("mentor_skills", table =>
        {
            table.HasCheckConstraint(
                "ck_mentor_skills_ids",
                "mentor_profile_id <> '00000000-0000-0000-0000-000000000000' AND skill_id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint(
                "ck_mentor_skills_proficiency_level",
                "proficiency_level IN ('Beginner', 'Intermediate', 'Advanced', 'Expert')");
            table.HasCheckConstraint("ck_mentor_skills_years_of_experience", "years_of_experience >= 0");
        });
        builder.HasKey(skill => skill.Id);
        builder.Ignore(skill => skill.DomainEvents);
        builder.Property(skill => skill.MentorProfileId).IsRequired();
        builder.Property(skill => skill.SkillId).IsRequired();
        builder.Property(skill => skill.SkillName).HasMaxLength(150).IsRequired();
        builder.Property(skill => skill.ProficiencyLevel).HasMaxLength(30).IsRequired();
        builder.Property(skill => skill.YearsOfExperience).IsRequired();
        builder.Property(skill => skill.CreatedAtUtc).IsRequired();
        builder.HasIndex(skill => new { skill.MentorProfileId, skill.SkillId })
            .IsUnique()
            .HasDatabaseName("ux_mentor_skills_profile_skill");
        builder.HasIndex(skill => skill.SkillId).HasDatabaseName("ix_mentor_skills_skill_id");
        builder.HasOne<MentorProfile>()
            .WithMany()
            .HasForeignKey(skill => skill.MentorProfileId)
            .OnDelete(DeleteBehavior.Cascade);
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
