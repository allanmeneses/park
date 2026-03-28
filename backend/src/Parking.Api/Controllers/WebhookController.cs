using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Domain;
using Parking.Infrastructure.Audit;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Api.Parking;

namespace Parking.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
public sealed class WebhookController(
    TenantDbContext db,
    AuditService audit,
    IConfiguration configuration,
    IHttpContextAccessor http) : ControllerBase
{
    private Guid ParkingId => (Guid)http.HttpContext!.Items[ParkingConstants.ParkingIdItem]!;

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        if (!Request.Headers.TryGetValue("X-Signature", out var sigHex))
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "Assinatura ausente." });

        var secret = configuration["PIX_WEBHOOK_SECRET"] ?? "";
        if (secret.Length < 32)
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "Segredo inválido." });

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expectedHex = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw)));
        var got = sigHex.ToString();
        if (expectedHex.Length != got.Length ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedHex), Encoding.UTF8.GetBytes(got.ToLowerInvariant())))
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "Assinatura inválida." });

        var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var transactionId = root.GetProperty("transaction_id").GetString()!;
        var paymentId = root.GetProperty("payment_id").GetGuid();
        var status = root.GetProperty("status").GetString();
        if (status != "PAID")
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Status inválido." });

        if (await db.WebhookReceipts.AsNoTracking().AnyAsync(w => w.TransactionId == transactionId, ct))
            return Ok(new { ok = true, duplicate = true });

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (p == null)
        {
            await tx.RollbackAsync(ct);
            return NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." });
        }

        if (p.Status == PaymentStatus.PAID)
        {
            await tx.CommitAsync(ct);
            return Ok(new { ok = true, ignored = true });
        }

        if (p.Status is PaymentStatus.EXPIRED or PaymentStatus.FAILED)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "WEBHOOK_LATE", message = "Pagamento expirado." });
        }

        if (p.Status != PaymentStatus.PENDING)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "INVALID_PAYMENT_STATE", message = "Estado inválido." });
        }

        p.Status = PaymentStatus.PAID;
        p.PaidAt = DateTimeOffset.UtcNow;
        p.Method = p.Method ?? PaymentMethod.PIX;
        if (p.TicketId is { } tid)
        {
            var t = await db.Tickets.FirstAsync(x => x.Id == tid, ct);
            t.Status = TicketStatus.CLOSED;
        }

        if (p.PackageOrderId is { } oid)
        {
            var ord = await db.PackageOrders.FirstAsync(o => o.Id == oid, ct);
            ord.Status = "PAID";
            ord.PaidAt = DateTimeOffset.UtcNow;
            var pkg = await db.RechargePackages.FirstAsync(pk => pk.Id == ord.PackageId, ct);
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
                    Settlement = "PIX",
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
                    Settlement = "PIX",
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            await audit.AppendAsync(ParkingId, "package", oid, "PACKAGE_PURCHASE",
                new { order_id = oid, package_id = pkg.Id, settlement = "PIX" }, ct);
        }

        db.WebhookReceipts.Add(new WebhookReceiptRow
        {
            TransactionId = transactionId,
            PaymentId = paymentId,
            ProcessedAt = DateTimeOffset.UtcNow
        });

        await audit.AppendAsync(ParkingId, "payment", p.Id, "PAYMENT",
            new { payment_id = p.Id, from_status = nameof(PaymentStatus.PENDING), to_status = nameof(PaymentStatus.PAID) }, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { ok = true });
    }
}
