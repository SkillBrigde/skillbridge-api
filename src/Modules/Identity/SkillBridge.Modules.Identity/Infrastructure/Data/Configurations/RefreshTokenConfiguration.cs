using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Modules.Identity.Domain;

namespace SkillBridge.Modules.Identity.Infrastructure.Data.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.Ignore("DomainEvents");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(t => t.SecurityStamp)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.RevokedAtUtc).IsConcurrencyToken();

        builder.HasIndex(t => t.Token)
            .IsUnique();

        builder.HasIndex(t => t.UserId);

        builder.Property(t => t.ExpiresAtUtc)
            .IsRequired();

        builder.Property(t => t.ReplacedByToken)
            .HasMaxLength(64);

        builder.Property(t => t.CreatedByIp)
            .HasMaxLength(50);

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
