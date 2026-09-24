using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Modules.Identity.Domain;

namespace SkillBridge.Modules.Identity.Infrastructure.Data.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.Ignore("DomainEvents");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.Property(r => r.NormalizedName)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.NormalizedName)
            .IsUnique();

        builder.Property(r => r.Description)
            .HasMaxLength(250);
    }
}
