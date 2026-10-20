using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Modules.Payments.Domain;

namespace SkillBridge.Modules.Payments.Infrastructure;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public const string Schema = "payments";
    public DbSet<PaymentOrder> Orders => Set<PaymentOrder>();
    public DbSet<PaymentEscrow> Escrows => Set<PaymentEscrow>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<GatewayReceipt> Receipts => Set<GatewayReceipt>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);
        model.HasSequence<long>("order_codes", Schema).StartsAt(1_000_000);
        model.Entity<PaymentOrder>(b =>
        {
            b.ToTable("payment_orders", t => t.HasCheckConstraint("ck_payment_amount", "amount > 0 AND amount <= 1000000000 AND currency = 'VND'"));
            b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.OrderCode).HasDefaultValueSql("nextval('payments.order_codes')");
            b.Property(x => x.Amount).HasPrecision(18, 0);
            b.Property(x => x.Gateway).HasMaxLength(10); b.Property(x => x.Status).HasMaxLength(30);
            b.Property(x => x.Currency).HasMaxLength(3); b.Property(x => x.GatewayReference).HasMaxLength(100);
            b.Property(x => x.IdempotencyKey).HasMaxLength(100); b.Property(x => x.RequestHash).HasMaxLength(64);
            b.Property(x => x.ReturnUrl).HasMaxLength(2048); b.Property(x => x.CancelUrl).HasMaxLength(2048);
            b.Property(x => x.LinkJson).HasColumnType("jsonb");
            b.HasIndex(x => x.OrderCode).IsUnique(); b.HasIndex(x => x.BookingId).IsUnique();
            b.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
            b.HasIndex(x => new { x.Gateway, x.GatewayReference }).IsUnique(); b.HasIndex(x => x.Status);
        });
        model.Entity<PaymentEscrow>(b =>
        {
            b.ToTable("escrows", t => t.HasCheckConstraint("ck_escrow_amount", "total_amount > 0 AND status IN ('Holding','ReviewRequired')"));
            b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TotalAmount).HasPrecision(18, 0); b.Property(x => x.Status).HasMaxLength(30); b.Property(x => x.Currency).HasMaxLength(3);
            b.HasIndex(x => x.PaymentId).IsUnique(); b.HasIndex(x => x.BookingId).IsUnique();
            b.HasOne<PaymentOrder>().WithMany().HasForeignKey(x => x.PaymentId);
        });
        model.Entity<LedgerEntry>(b =>
        {
            b.ToTable("ledger_entries", t => t.HasCheckConstraint("ck_ledger_account", "(account = 'GatewayClearing' AND amount > 0) OR (account = 'EscrowLiability' AND amount < 0)"));
            b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Account).HasMaxLength(30); b.Property(x => x.Amount).HasPrecision(18, 0);
            b.HasIndex(x => new { x.PaymentId, x.Account }).IsUnique(); b.HasOne<PaymentOrder>().WithMany().HasForeignKey(x => x.PaymentId);
        });
        model.Entity<Wallet>(b => { b.ToTable("wallets"); b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever(); });
        model.Entity<GatewayReceipt>(b =>
        {
            b.ToTable("gateway_receipts"); b.HasKey(x => x.Id); b.Ignore(x => x.DomainEvents); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Gateway).HasMaxLength(10); b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Currency).HasMaxLength(3); b.Property(x => x.Amount).HasPrecision(18, 0); b.Property(x => x.RejectionCode).HasMaxLength(100);
            b.HasIndex(x => new { x.Gateway, x.Reference }).IsUnique();
        });
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties()) property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}
