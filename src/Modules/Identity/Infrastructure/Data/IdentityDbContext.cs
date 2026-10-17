using Microsoft.EntityFrameworkCore;
using SkillBridge.Modules.Identity.Domain;

namespace SkillBridge.Modules.Identity.Infrastructure.Data;

public sealed class IdentityDbContext : DbContext
{
    public const string Schema = "identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Đặt schema độc lập cho Module Identity
        modelBuilder.HasDefaultSchema(Schema);

        // Tự động quét và áp dụng tất cả IEntityTypeConfiguration trong assembly của Module Identity
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
