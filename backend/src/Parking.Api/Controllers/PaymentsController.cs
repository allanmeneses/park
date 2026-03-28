using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Domain;
using Parking.Infrastructure.Audit;
using Parking.Api.Parking;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Pix;

namespace Parking.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize(Roles = $"{nameof(UserRole.OPERATOR)},{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)},{nameof(UserRole.CLIENT)},{nameof(UserRole.LOJISTA)}")]
public sealed class PaymentsController(
    TenantDbContext db,
    AuditService audit,
    IPixPaymentAdapter pix,
    IConfiguration configuration,
    IHttpContextAccessor http) : ControllerBase
{
    private Guid ParkingId => (Guid)http.HttpContext!.Items[ParkingConstants.ParkingIdItem]!;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var entityId = User.FindFirst("entity_id")?.Value;
        var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." });

        if (role is nameof(UserRole.CLIENT) or nameof(UserRole.LOJISTA))
        {
            if (p.PackageOrderId is null)
                return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });
            var order = await db.PackageOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == p.PackageOrderId, ct);
            if (order == null)
                return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });
            if (role == nameof(UserRole.CLIENT) && order.ClientId?.ToString() != entityId)
                return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });
            if (role == nameof(UserRole.LOJISTA) && order.LojistaId?.ToString() != entityId)
                return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });
        }

        var pixRow = await db.PixTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentId == id && x.Active, ct);
        object? pixObj = null;
        if (pixRow != null)
            pixObj = new { expires_at = pixRow.ExpiresAt, active = true };

        return Ok(new
        {
            id = p.Id,
            status = p.Status.ToString(),
            method = p.Method?.ToString(),
            amount = p.Amount.ToString("0.00"),
            ticket_id = p.TicketId,
            package_order_id = p.PackageOrderId,
            paid_at = p.PaidAt,
            created_at = p.CreatedAt,
            failed_reason = p.FailedReason,
            pix = pixObj
        });
    }

    [Authorize(Roles = $"{nameof(UserRole.OPERATOR)},{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpPost("pix")]
    public async Task<IActionResult> Pix([FromBody] PixRequest body, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == body.PaymentId, ct);
        if (p == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." });

        if (p.Status == PaymentStatus.PAID)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "PAYMENT_ALREADY_PAID", message = "Já pago." });
        }

        if (p.Status == PaymentStatus.EXPIRED)
        {
            p.Status = PaymentStatus.PENDING;
            p.FailedReason = null;
        }

        if (p.PackageOrderId is { } poId)
        {
            var ord = await db.PackageOrders.FirstOrDefaultAsync(o => o.Id == poId, ct);
            if (ord == null || ord.Status != "AWAITING_PAYMENT")
            {
                await tx.RollbackAsync(ct);
                return Conflict(new { code = "INVALID_TICKET_STATE", message = "Pedido inválido." });
            }
        }

        p.Method = PaymentMethod.PIX;
        var ttl = int.TryParse(configuration["PIX_DEFAULT_TTL_SECONDS"], out var s) ? s : 300;
        var existing = await db.PixTransactions.Where(x => x.PaymentId == p.Id && x.Active).ToListAsync(ct);
        var active = existing.FirstOrDefault(x => x.ExpiresAt > DateTimeOffset.UtcNow);
        if (active != null)
        {
            await tx.CommitAsync(ct);
            return Ok(new { payment_id = p.Id, qr_code = active.QrCode, expires_at = active.ExpiresAt });
        }

        foreach (var x in existing.Where(x => x.ExpiresAt <= DateTimeOffset.UtcNow))
            x.Active = false;

        var charge = await pix.CreateChargeAsync(p.Id, p.Amount, ttl, ct);
        var pixRow = new PixTransactionRow
        {
            Id = Guid.NewGuid(),
            PaymentId = p.Id,
            ProviderStatus = "CREATED",
            QrCode = charge.QrCode,
            ExpiresAt = charge.ExpiresAt,
            TransactionId = charge.ProviderTransactionId,
            Active = true
        };
        db.PixTransactions.Add(pixRow);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { payment_id = p.Id, qr_code = charge.QrCode, expires_at = charge.ExpiresAt });
    }

    [Authorize(Roles = $"{nameof(UserRole.OPERATOR)},{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpPost("card")]
    public async Task<IActionResult> Card([FromBody] CardRequest body, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == body.PaymentId, ct);
        if (p == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." });
        if (p.Amount != body.Amount)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "AMOUNT_MISMATCH", message = "Valor divergente." });
        }

        p.Method = PaymentMethod.CARD;
        p.Status = PaymentStatus.PAID;
        p.PaidAt = DateTimeOffset.UtcNow;
        if (p.TicketId is { } tid)
        {
            var t = await db.Tickets.FirstAsync(x => x.Id == tid, ct);
            t.Status = TicketStatus.CLOSED;
        }

        if (p.PackageOrderId is { } oid)
            await CompletePackageOrderAsync(oid, ct);

        await db.SaveChangesAsync(ct);
        await audit.AppendAsync(ParkingId, "payment", p.Id, "PAYMENT", new { payment_id = p.Id, from_status = nameof(PaymentStatus.PENDING), to_status = nameof(PaymentStatus.PAID) }, ct);
        await tx.CommitAsync(ct);
        return Ok(new { payment_id = p.Id, status = nameof(PaymentStatus.PAID) });
    }

    [Authorize(Roles = $"{nameof(UserRole.OPERATOR)},{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpPost("cash")]
    public async Task<IActionResult> Cash([FromBody] CashPayRequest body, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var open = await db.CashSessions.FirstOrDefaultAsync(s => s.Status == CashSessionStatus.OPEN, ct);
        if (open == null)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "CASH_SESSION_REQUIRED", message = "Caixa fechado." });
        }

        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == body.PaymentId, ct);
        if (p == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." });

        p.Method = PaymentMethod.CASH;
        p.Status = PaymentStatus.PAID;
        p.PaidAt = DateTimeOffset.UtcNow;
        open.ExpectedAmount += p.Amount;
        if (p.TicketId is { } tid)
        {
            var t = await db.Tickets.FirstAsync(x => x.Id == tid, ct);
            t.Status = TicketStatus.CLOSED;
        }

        if (p.PackageOrderId is { } oid)
            await CompletePackageOrderAsync(oid, ct);

        await db.SaveChangesAsync(ct);
        await audit.AppendAsync(ParkingId, "payment", p.Id, "PAYMENT", new { payment_id = p.Id, from_status = nameof(PaymentStatus.PENDING), to_status = nameof(PaymentStatus.PAID) }, ct);
        await tx.CommitAsync(ct);
        return Ok(new { payment_id = p.Id, status = nameof(PaymentStatus.PAID) });
    }

    private async Task CompletePackageOrderAsync(Guid orderId, CancellationToken ct)
    {
        var ord = await db.PackageOrders.FirstAsync(o => o.Id == orderId, ct);
        ord.Status = "PAID";
        ord.PaidAt = DateTimeOffset.UtcNow;
        var pkg = await db.RechargePackages.FirstAsync(p => p.Id == ord.PackageId, ct);
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
                Settlement = ord.Settlement,
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
                Settlement = ord.Settlement,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await audit.AppendAsync(ParkingId, "package", orderId, "PACKAGE_PURCHASE", new { order_id = orderId, package_id = pkg.Id, settlement = ord.Settlement }, ct);
    }

    public sealed record PixRequest([property: System.Text.Json.Serialization.JsonPropertyName("payment_id")] Guid PaymentId);
    public sealed record CardRequest(Guid PaymentId, decimal Amount);
    public sealed record CashPayRequest(Guid PaymentId);
}
