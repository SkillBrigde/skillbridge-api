using Microsoft.EntityFrameworkCore;
using SkillBridge.BuildingBlocks.Events;
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
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task RevokeSessionsAsync(Guid userId, string expectedSecurityStamp, CancellationToken cancellationToken)
    {
        var securityStamp = Guid.NewGuid().ToString("N");
        var revokedAtUtc = DateTimeOffset.UtcNow;
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var updated = await Users.Where(user => user.Id == userId && user.SecurityStamp == expectedSecurityStamp)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.SecurityStamp, securityStamp)
                .SetProperty(user => user.UpdatedAtUtc, revokedAtUtc), cancellationToken);
        if (updated == 0)
        {
            return;
        }

        await RefreshTokens.Where(token => token.UserId == userId && token.SecurityStamp == expectedSecurityStamp && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAtUtc, revokedAtUtc), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Đặt schema độc lập cho Module Identity
        modelBuilder.HasDefaultSchema(Schema);

        // Tự động quét và áp dụng tất cả IEntityTypeConfiguration trong assembly của Module Identity
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Type).HasMaxLength(200).IsRequired();
            builder.Property(message => message.Content).HasColumnType("jsonb").IsRequired();
            builder.HasIndex(message => message.ProcessedOnUtc);
            builder.Ignore("DomainEvents");
        });
    }
}
