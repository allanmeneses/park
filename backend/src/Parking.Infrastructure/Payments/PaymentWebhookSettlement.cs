using Microsoft.EntityFrameworkCore;
using Parking.Domain;
using Parking.Infrastructure.Audit;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Infrastructure.Payments;

public enum PaymentWebhookSettlementStatus
{
    Ok,
    Duplicate,
    IgnoredAlreadyPaid,
    NotFound,
    InvalidState,
    Late
}

/// <summary>Lógica comum para marcar pagamento pago (webhook interno ou PSP).</summary>
public sealed class PaymentWebhookSettlement(TenantDbContext db, AuditService audit)
{
    public async Task<PaymentWebhookSettlementStatus> TryMarkPaidAsync(
        Guid parkingId,
        Guid paymentId,
        string externalTransactionId,
        PaymentMethod method,
        CancellationToken ct)
    {
        if (await db.WebhookReceipts.AsNoTracking().AnyAsync(w => w.TransactionId == externalTransactionId, ct))
            return PaymentWebhookSettlementStatus.Duplicate;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (p == null)
        {
            await tx.RollbackAsync(ct);
            return PaymentWebhookSettlementStatus.NotFound;
        }

        if (p.Status == PaymentStatus.PAID)
        {
            await tx.CommitAsync(ct);
            return PaymentWebhookSettlementStatus.IgnoredAlreadyPaid;
        }

        if (p.Status is PaymentStatus.EXPIRED or PaymentStatus.FAILED)
        {
            await tx.RollbackAsync(ct);
            return PaymentWebhookSettlementStatus.Late;
        }

        if (p.Status != PaymentStatus.PENDING)
        {
            await tx.RollbackAsync(ct);
            return PaymentWebhookSettlementStatus.InvalidState;
        }

        p.Status = PaymentStatus.PAID;
        p.PaidAt = DateTimeOffset.UtcNow;
        p.Method = method;
        if (p.TicketId is { } tid)
        {
            var t = await db.Tickets.FirstAsync(x => x.Id == tid, ct);
            t.Status = TicketStatus.CLOSED;
        }

        if (p.PackageOrderId is { } oid)
            await CompletePackageOrderAsync(parkingId, oid, ct);

        db.WebhookReceipts.Add(new WebhookReceiptRow
        {
            TransactionId = externalTransactionId,
            PaymentId = paymentId,
            ProcessedAt = DateTimeOffset.UtcNow
        });

        await audit.AppendAsync(parkingId, "payment", p.Id, "PAYMENT",
            new { payment_id = p.Id, from_status = nameof(PaymentStatus.PENDING), to_status = nameof(PaymentStatus.PAID) }, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return PaymentWebhookSettlementStatus.Ok;
    }

    private async Task CompletePackageOrderAsync(Guid parkingId, Guid orderId, CancellationToken ct)
    {
        var ord = await db.PackageOrders.FirstAsync(o => o.Id == orderId, ct);
        ord.Status = "PAID";
        ord.PaidAt = DateTimeOffset.UtcNow;
        var pkg = await db.RechargePackages.FirstAsync(p => p.Id == ord.PackageId, ct);
        var settlement = ord.Settlement ?? "PIX";
        if (ord.Scope == "CLIENT" && ord.ClientId is { } cid)
        {
            var w = await db.ClientWallets.FirstOrDefaultAsync(x => x.ClientId == cid, ct);
            if (w == null)
            {
                w = new ClientWalletRow { Id = Guid.NewGuid(), ClientId = cid, BalanceHours = 0, ExpirationDate = null };
                db.ClientWallets.Add(w);
            }

            w.BalanceHours += pkg.Hours;
            db.WalletLedger.Add(new WalletLedgerRow
            {
                Id = Guid.NewGuid(),
                ClientId = cid,
                LojistaId = null,
                DeltaHours = pkg.Hours,
                Amount = ord.Amount,
                PackageId = pkg.Id,
                Settlement = settlement,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else if (ord.Scope == "LOJISTA" && ord.LojistaId is { } lid)
        {
            var w = await db.LojistaWallets.FirstOrDefaultAsync(x => x.LojistaId == lid, ct);
            if (w == null)
            {
                w = new LojistaWalletRow { Id = Guid.NewGuid(), LojistaId = lid, BalanceHours = 0 };
                db.LojistaWallets.Add(w);
            }

            w.BalanceHours += pkg.Hours;
            db.WalletLedger.Add(new WalletLedgerRow
            {
                Id = Guid.NewGuid(),
                ClientId = null,
                LojistaId = lid,
                DeltaHours = pkg.Hours,
                Amount = ord.Amount,
                PackageId = pkg.Id,
                Settlement = settlement,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await audit.AppendAsync(parkingId, "package", orderId, "PACKAGE_PURCHASE",
            new { order_id = orderId, package_id = pkg.Id, settlement }, ct);
    }
}
