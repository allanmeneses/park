using System.ComponentModel.DataAnnotations.Schema;
using Parking.Domain;

namespace Parking.Infrastructure.Persistence.Tenant;

[Table("settings")]
public class SettingsRow
{
    public Guid Id { get; set; }
    public decimal PricePerHour { get; set; }
    public int Capacity { get; set; }
    public bool LojistaGrantSameDayOnly { get; set; }
}

[Table("tickets")]
public class TicketRow
{
    public Guid Id { get; set; }
    public string Plate { get; set; } = "";
    public DateTimeOffset EntryTime { get; set; }
    public DateTimeOffset? ExitTime { get; set; }
    public TicketStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

[Table("lojistas")]
public class LojistaRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal HourPrice { get; set; }

    /// <summary>
    /// Quando <c>false</c>, <c>POST /lojista/grant-client</c> só permite bonificar se existir ticket
    /// <c>OPEN</c> ou <c>AWAITING_PAYMENT</c> para a placa (ou o ticket referenciado).
    /// Quando <c>true</c> (padrão), permite crédito antecipado só com a placa.
    /// </summary>
    public bool AllowGrantBeforeEntry { get; set; } = true;
}

[Table("lojista_wallets")]
public class LojistaWalletRow
{
    public Guid Id { get; set; }
    public Guid LojistaId { get; set; }
    public int BalanceHours { get; set; }
}

[Table("clients")]
public class ClientRow
{
    public Guid Id { get; set; }
    public string Plate { get; set; } = "";
    public Guid? LojistaId { get; set; }
}

[Table("client_wallets")]
public class ClientWalletRow
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public int BalanceHours { get; set; }
    public DateTimeOffset? ExpirationDate { get; set; }
}

[Table("wallet_usages")]
public class WalletUsageRow
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string Source { get; set; } = "";
    public int HoursUsed { get; set; }
}

[Table("recharge_packages")]
public class RechargePackageRow
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Scope { get; set; } = "";
    public int Hours { get; set; }
    public decimal Price { get; set; }
    public bool IsPromo { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public bool Active { get; set; } = true;
}

[Table("package_orders")]
public class PackageOrderRow
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = "";
    public Guid? ClientId { get; set; }
    public Guid? LojistaId { get; set; }
    public Guid PackageId { get; set; }
    public string Status { get; set; } = "";
    public string Settlement { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
}

[Table("payments")]
public class PaymentRow
{
    public Guid Id { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? PackageOrderId { get; set; }
    public PaymentMethod? Method { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string? TransactionId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? FailedReason { get; set; }
}

[Table("pix_transactions")]
public class PixTransactionRow
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public string ProviderStatus { get; set; } = "";
    public string QrCode { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public string? TransactionId { get; set; }
    public bool Active { get; set; } = true;
}

[Table("cash_sessions")]
public class CashSessionRow
{
    public Guid Id { get; set; }
    public CashSessionStatus Status { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
}

[Table("operator_events")]
public class OperatorEventRow
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

[Table("wallet_ledger")]
public class WalletLedgerRow
{
    public Guid Id { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? LojistaId { get; set; }
    public int DeltaHours { get; set; }
    public decimal Amount { get; set; }
    public Guid? PackageId { get; set; }
    public string? Settlement { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

[Table("alerts")]
public class AlertRow
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

[Table("schema_migrations")]
public class SchemaMigrationRow
{
    public string Version { get; set; } = "";
    public DateTimeOffset AppliedAt { get; set; }
}

[Table("idempotency_store")]
public class IdempotencyStoreRow
{
    public string Key { get; set; } = "";
    public string Route { get; set; } = "";
    public string ResponseJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

[Table("webhook_receipts")]
public class WebhookReceiptRow
{
    public string TransactionId { get; set; } = "";
    public Guid PaymentId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}
