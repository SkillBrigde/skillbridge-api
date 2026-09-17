using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Modules.Identity.Domain;

namespace SkillBridge.Modules.Identity.Infrastructure.Data.Configurations;

internal sealed class SecurityAuditEntryConfiguration : IEntityTypeConfiguration<SecurityAuditEntry>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEntry> builder)
    {
        builder.ToTable("security_audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.SubjectId, x.CreatedAtUtc });
        builder.Ignore(x => x.DomainEvents);
    }
}
