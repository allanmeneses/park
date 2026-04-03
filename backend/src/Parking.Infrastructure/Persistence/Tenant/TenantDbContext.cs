using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using Parking.Domain;

namespace Parking.Infrastructure.Persistence.Tenant;

public class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
{
    private static readonly NpgsqlNullNameTranslator EnumNames = new();
    public DbSet<SettingsRow> Settings => Set<SettingsRow>();
    public DbSet<TicketRow> Tickets => Set<TicketRow>();
    public DbSet<LojistaRow> Lojistas => Set<LojistaRow>();
    public DbSet<LojistaWalletRow> LojistaWallets => Set<LojistaWalletRow>();
    public DbSet<ClientRow> Clients => Set<ClientRow>();
    public DbSet<ClientWalletRow> ClientWallets => Set<ClientWalletRow>();
    public DbSet<WalletUsageRow> WalletUsages => Set<WalletUsageRow>();
    public DbSet<RechargePackageRow> RechargePackages => Set<RechargePackageRow>();
    public DbSet<PackageOrderRow> PackageOrders => Set<PackageOrderRow>();
    public DbSet<PaymentRow> Payments => Set<PaymentRow>();
    public DbSet<PixTransactionRow> PixTransactions => Set<PixTransactionRow>();
    public DbSet<CashSessionRow> CashSessions => Set<CashSessionRow>();
    public DbSet<OperatorEventRow> OperatorEvents => Set<OperatorEventRow>();
    public DbSet<WalletLedgerRow> WalletLedger => Set<WalletLedgerRow>();
    public DbSet<AlertRow> Alerts => Set<AlertRow>();
    public DbSet<SchemaMigrationRow> SchemaMigrations => Set<SchemaMigrationRow>();
    public DbSet<IdempotencyStoreRow> IdempotencyStore => Set<IdempotencyStoreRow>();
    public DbSet<WebhookReceiptRow> WebhookReceipts => Set<WebhookReceiptRow>();
    public DbSet<LojistaGrantRow> LojistaGrants => Set<LojistaGrantRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<TicketStatus>("public", "ticket_status", EnumNames);
        modelBuilder.HasPostgresEnum<PaymentStatus>("public", "payment_status", EnumNames);
        modelBuilder.HasPostgresEnum<PaymentMethod>("public", "payment_method", EnumNames);
        modelBuilder.HasPostgresEnum<CashSessionStatus>("public", "cash_session_status", EnumNames);

        modelBuilder.Entity<SettingsRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PricePerHour).HasPrecision(10, 2);
            e.Property(x => x.Capacity).IsRequired();
        });

        modelBuilder.Entity<TicketRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Plate).HasMaxLength(10).IsRequired();
            e.Property(x => x.Status).HasColumnType("ticket_status");
            e.HasIndex(x => x.Plate)
                .IsUnique()
                .HasFilter("status IN ('OPEN', 'AWAITING_PAYMENT')");
        });

        modelBuilder.Entity<LojistaRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.HourPrice).HasPrecision(10, 2);
            e.Property(x => x.AllowGrantBeforeEntry).HasDefaultValue(true);
        });

        modelBuilder.Entity<LojistaWalletRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LojistaId).IsUnique();
        });

        modelBuilder.Entity<ClientRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Plate).IsUnique();
            e.Property(x => x.Plate).HasMaxLength(10).IsRequired();
        });

        modelBuilder.Entity<ClientWalletRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ClientId).IsUnique();
        });

        modelBuilder.Entity<WalletUsageRow>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<RechargePackageRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<PackageOrderRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(10, 2);
        });

        modelBuilder.Entity<PaymentRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasColumnType("payment_status");
            e.Property(x => x.Method).HasColumnType("payment_method").IsRequired(false);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.HasIndex(x => x.TicketId).IsUnique().HasFilter("ticket_id IS NOT NULL");
            e.HasIndex(x => x.PackageOrderId).IsUnique().HasFilter("package_order_id IS NOT NULL");
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
        });

        modelBuilder.Entity<PixTransactionRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TransactionId).IsUnique();
            e.HasIndex(x => x.PaymentId)
                .IsUnique()
                .HasFilter("active = true");
        });

        modelBuilder.Entity<CashSessionRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasColumnType("cash_session_status");
            e.Property(x => x.ExpectedAmount).HasPrecision(10, 2);
            e.Property(x => x.ActualAmount).HasPrecision(10, 2);
        });

        modelBuilder.Entity<OperatorEventRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        modelBuilder.Entity<WalletLedgerRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(10, 2);
        });

        modelBuilder.Entity<AlertRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Payload).HasColumnType("jsonb");
        });

        modelBuilder.Entity<SchemaMigrationRow>(e =>
        {
            e.HasKey(x => x.Version);
        });

        modelBuilder.Entity<IdempotencyStoreRow>(e =>
        {
            e.HasKey(x => new { x.Key, x.Route });
            e.Property(x => x.ResponseJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<WebhookReceiptRow>(e =>
        {
            e.HasKey(x => x.TransactionId);
            e.HasIndex(x => x.ProcessedAt);
        });

        modelBuilder.Entity<LojistaGrantRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Plate).HasMaxLength(10).IsRequired();
            e.Property(x => x.GrantMode).HasMaxLength(16).IsRequired().HasDefaultValue("ADVANCE");
            e.HasIndex(x => new { x.LojistaId, x.CreatedAt });
        });
    }
}

