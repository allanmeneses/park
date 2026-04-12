using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Application.Checkout;
using Parking.Application.Validation;
using Parking.Domain;
using Parking.Api.Parking;
using Parking.Infrastructure.Audit;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Api.Controllers;

[ApiController]
[Route("api/v1/tickets")]
[Authorize(Roles = $"{nameof(UserRole.OPERATOR)},{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
public sealed class TicketsController(
    TenantDbContext db,
    AuditService audit,
    IHttpContextAccessor http) : ControllerBase
{
    private Guid ParkingId => (Guid)http.HttpContext!.Items[ParkingConstants.ParkingIdItem]!;

    private const string RouteCreate = "POST|/api/v1/tickets";
    private const string RouteCheckout = "POST|/api/v1/tickets/{id}/checkout";

    [HttpPost]
    public async Task<IActionResult> Create([FromHeader(Name = "Idempotency-Key")] string? idemKey, [FromBody] CreateTicketRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idemKey))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Idempotency-Key obrigatório." });
        var plate = PlateValidator.Normalize(body.Plate);
        if (!PlateValidator.IsValidNormalized(plate))
            return BadRequest(new { code = "PLATE_INVALID", message = "Placa inválida." });

        var cached = await db.IdempotencyStore.AsNoTracking().FirstOrDefaultAsync(
            x => x.Key == idemKey && x.Route == RouteCreate, ct);
        if (cached != null)
            return Content(cached.ResponseJson, "application/json");

        var active = await db.Tickets.AsNoTracking().AnyAsync(
            t => t.Plate == plate && (t.Status == TicketStatus.OPEN || t.Status == TicketStatus.AWAITING_PAYMENT), ct);
        if (active)
            return Conflict(new { code = "PLATE_HAS_ACTIVE_TICKET", message = "Já existe ticket aberto para esta placa." });

        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var ticket = new TicketRow
        {
            Id = id,
            Plate = plate,
            EntryTime = now,
            ExitTime = null,
            Status = TicketStatus.OPEN,
            CreatedAt = now
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        await audit.AppendAsync(ParkingId, "ticket", id, "TICKET_CREATE", new { ticket }, ct);

        var payload = JsonSerializer.Serialize(new
        {
            id,
            plate,
            status = nameof(TicketStatus.OPEN),
            entry_time = now
        });
        db.IdempotencyStore.Add(new IdempotencyStoreRow
        {
            Key = idemKey,
            Route = RouteCreate,
            ResponseJson = payload,
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);

        return Created($"/api/v1/tickets/{id}", JsonSerializer.Deserialize<JsonElement>(payload));
    }

    [HttpGet("open")]
    public async Task<IActionResult> Open(CancellationToken ct)
    {
        var items = await db.Tickets.AsNoTracking()
            .Where(t => t.Status == TicketStatus.OPEN || t.Status == TicketStatus.AWAITING_PAYMENT)
            .Select(t => new { t.Id, t.Plate, t.EntryTime, status = t.Status.ToString() })
            .ToListAsync(ct);
        return Ok(new { items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var t = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t == null)
            return NotFound(new { code = "NOT_FOUND", message = "Ticket não encontrado." });

        var pay = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.TicketId == id, ct);
        object? paymentDto = null;
        if (pay != null)
            paymentDto = await BuildPaymentDto(db, pay.Id, ct);

        var benefitRows = await LojistaBonificadoBalance.ListBenefitsVisibleOnTicketAsync(db, t.Plate, ct);
        var lojistaBenefits = benefitRows.Select(b => new
        {
            lojistaId = b.LojistaId,
            lojistaName = b.LojistaName,
            hoursAvailable = b.HoursAvailable,
            hoursGrantedTotal = b.HoursGrantedTotal
        }).ToList();

        return Ok(new
        {
            ticket = new
            {
                t.Id,
                t.Plate,
                t.EntryTime,
                t.ExitTime,
                status = t.Status.ToString(),
                t.CreatedAt
            },
            payment = paymentDto,
            lojistaBenefits
        });
    }

    [HttpPost("{id:guid}/checkout")]
    public async Task<IActionResult> Checkout(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idemKey,
        [FromHeader(Name = "X-Device-Time")] string? deviceTime,
        [FromBody] CheckoutRequest? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idemKey))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Idempotency-Key obrigatório." });
        var route = RouteCheckout.Replace("{id}", id.ToString(), StringComparison.Ordinal);
        var cached = await db.IdempotencyStore.AsNoTracking().FirstOrDefaultAsync(
            x => x.Key == idemKey && x.Route == route, ct);
        if (cached != null)
            return Content(cached.ResponseJson, "application/json");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket == null)
        {
            await tx.RollbackAsync(ct);
            return NotFound(new { code = "NOT_FOUND", message = "Ticket não encontrado." });
        }

        PaymentRow? existingPayment = null;
        var isRecalculation = ticket.Status == TicketStatus.AWAITING_PAYMENT;
        if (ticket.Status is not (TicketStatus.OPEN or TicketStatus.AWAITING_PAYMENT))
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "INVALID_TICKET_STATE", message = "Estado de ticket inválido." });
        }
        if (isRecalculation)
        {
            existingPayment = await db.Payments.FirstOrDefaultAsync(p => p.TicketId == ticket.Id, ct);
            if (existingPayment is null)
            {
                await tx.RollbackAsync(ct);
                return Conflict(new { code = "INVALID_TICKET_STATE", message = "Estado de ticket inválido." });
            }

            if (existingPayment.Status == PaymentStatus.PAID)
            {
                await tx.RollbackAsync(ct);
                return Conflict(new { code = "INVALID_TICKET_STATE", message = "Estado de ticket inválido." });
            }

            // Alinhar a POST /payments/pix: PIX expirado / falhou → voltar a PENDING para novo QR ou novo valor.
            if (existingPayment.Status is PaymentStatus.EXPIRED or PaymentStatus.FAILED)
            {
                existingPayment.Status = PaymentStatus.PENDING;
                existingPayment.FailedReason = null;
            }
            else if (existingPayment.Status != PaymentStatus.PENDING)
            {
                await tx.RollbackAsync(ct);
                return Conflict(new { code = "INVALID_TICKET_STATE", message = "Estado de ticket inválido." });
            }

            // Reverte consumos anteriores deste ticket antes de recalcular.
            var existingUsages = await db.WalletUsages.Where(x => x.TicketId == ticket.Id).ToListAsync(ct);
            var revertClientHours = existingUsages.Where(x => x.Source == "client").Sum(x => x.HoursUsed);
            if (revertClientHours > 0)
            {
                var clientRevert = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Plate == ticket.Plate, ct);
                if (clientRevert != null)
                {
                    var cwRevert = await db.ClientWallets.FirstOrDefaultAsync(w => w.ClientId == clientRevert.Id, ct);
                    if (cwRevert != null)
                        cwRevert.BalanceHours += revertClientHours;
                }
            }
            if (existingUsages.Count > 0)
                db.WalletUsages.RemoveRange(existingUsages);

            // Persistir já: SumGrantScopedLojistaUsedHoursAsync lê a BD; sem flush, os usos `lojista` deste ticket
            // continuariam visíveis e consumiriam o saldo bonificado duas vezes (valor cheio em vez da diferença).
            await db.SaveChangesAsync(ct);
        }

        // Recálculo (pagamento pendente): sem exit_time no corpo, usa o instante atual do servidor — o cliente pode
        // ter permanecido no pátio após a primeira saída registada / desistência do pagamento.
        DateTimeOffset exit;
        if (isRecalculation)
            exit = body?.ExitTime ?? DateTimeOffset.UtcNow;
        else
            exit = body?.ExitTime ?? ticket.ExitTime ?? DateTimeOffset.UtcNow;
        if (exit < ticket.EntryTime)
        {
            await tx.RollbackAsync(ct);
            return BadRequest(new { code = "VALIDATION_ERROR", message = "exit_time inválido." });
        }

        if (body?.ExitTime is not null && !string.IsNullOrWhiteSpace(deviceTime))
        {
            if (!DateTimeOffset.TryParse(
                    deviceTime,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var devNow))
            {
                await tx.RollbackAsync(ct);
                return BadRequest(new { code = "VALIDATION_ERROR", message = "X-Device-Time inválido." });
            }

            var skew = Math.Abs((devNow - DateTimeOffset.UtcNow).TotalSeconds);
            if (skew > 300)
            {
                await tx.RollbackAsync(ct);
                return BadRequest(new { code = "CLOCK_SKEW", message = "Relógio do dispositivo divergente do servidor." });
            }
        }

        var hoursTotal = CheckoutMath.ComputeBillableHours(ticket.EntryTime, exit);
        var settings = await EnsureSettingsAsync(db, ct);
        var price = settings.PricePerHour;

        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Plate == ticket.Plate, ct);
        var horasLojista = 0;
        var horasCliente = 0;

        var horasRestantes = hoursTotal;
        var saldoBonificado =
            await LojistaBonificadoBalance.PlateAvailableBonificadoHoursAsync(db, ticket.Plate, ct);
        horasLojista = Math.Min(horasRestantes, saldoBonificado);
        if (horasLojista > 0)
        {
            db.WalletUsages.Add(new WalletUsageRow
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                Source = "lojista",
                HoursUsed = horasLojista
            });
            horasRestantes -= horasLojista;
        }

        if (client != null)
        {
            var cw = await db.ClientWallets.FirstOrDefaultAsync(w => w.ClientId == client.Id, ct);
            var saldo = 0;
            if (cw != null)
            {
                if (cw.ExpirationDate == null || cw.ExpirationDate >= DateTimeOffset.UtcNow)
                    saldo = cw.BalanceHours;
            }

            horasCliente = Math.Min(horasRestantes, saldo);
            if (horasCliente > 0 && cw != null)
            {
                cw.BalanceHours -= horasCliente;
                db.WalletUsages.Add(new WalletUsageRow
                {
                    Id = Guid.NewGuid(),
                    TicketId = ticket.Id,
                    Source = "client",
                    HoursUsed = horasCliente
                });
            }
        }

        horasRestantes -= horasCliente;
        var horasPagaveis = Math.Max(0, horasRestantes);
        var amount = CheckoutMath.RoundMoney(horasPagaveis * price);

        var paymentId = existingPayment?.Id ?? Guid.NewGuid();
        if (existingPayment is null)
        {
            var idemPay = Guid.NewGuid().ToString();
            db.Payments.Add(new PaymentRow
            {
                Id = paymentId,
                TicketId = ticket.Id,
                PackageOrderId = null,
                Method = null,
                Status = amount == 0 ? PaymentStatus.PAID : PaymentStatus.PENDING,
                Amount = amount,
                TransactionId = null,
                IdempotencyKey = idemPay,
                CreatedAt = DateTimeOffset.UtcNow,
                PaidAt = amount == 0 ? DateTimeOffset.UtcNow : null,
                FailedReason = null
            });
        }
        else
        {
            existingPayment.Method = null;
            existingPayment.Status = amount == 0 ? PaymentStatus.PAID : PaymentStatus.PENDING;
            existingPayment.Amount = amount;
            existingPayment.TransactionId = null;
            existingPayment.PaidAt = amount == 0 ? DateTimeOffset.UtcNow : null;
            existingPayment.FailedReason = null;
        }

        ticket.ExitTime = exit;
        ticket.Status = amount == 0 ? TicketStatus.CLOSED : TicketStatus.AWAITING_PAYMENT;

        await db.SaveChangesAsync(ct);

        await audit.AppendAsync(ParkingId, "ticket", ticket.Id, "CHECKOUT", new
        {
            ticket_id = ticket.Id,
            exit_time = exit,
            hours_total = hoursTotal,
            hours_lojista = horasLojista,
            hours_cliente = horasCliente,
            amount,
            payment_id = paymentId
        }, ct);

        if (amount > 0)
            await audit.AppendAsync(ParkingId, "payment", paymentId, "PAYMENT", new { payment_id = paymentId, from_status = "NEW", to_status = nameof(PaymentStatus.PENDING) }, ct);
        else
            await audit.AppendAsync(ParkingId, "payment", paymentId, "PAYMENT", new { payment_id = paymentId, from_status = "NEW", to_status = nameof(PaymentStatus.PAID) }, ct);

        var resp = JsonSerializer.Serialize(new
        {
            ticket_id = id,
            hours_total = hoursTotal,
            hours_lojista = horasLojista,
            hours_cliente = horasCliente,
            hours_paid = horasPagaveis,
            amount = MoneyFormatting.Format(amount),
            payment_id = paymentId
        });
        db.IdempotencyStore.Add(new IdempotencyStoreRow
        {
            Key = idemKey,
            Route = route,
            ResponseJson = resp,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Content(resp, "application/json");
    }

    private static readonly Guid SettingsSingletonId = Guid.Parse("00000000-0000-0000-0000-000000000000");

    private static async Task<SettingsRow> EnsureSettingsAsync(TenantDbContext db, CancellationToken ct)
    {
        var s = await db.Settings.FirstOrDefaultAsync(x => x.Id == SettingsSingletonId, ct);
        if (s != null) return s;
        s = new SettingsRow { Id = SettingsSingletonId, PricePerHour = 5.00m, Capacity = 50, LojistaGrantSameDayOnly = false };
        db.Settings.Add(s);
        await db.SaveChangesAsync(ct);
        return s;
    }

    private static async Task<object?> BuildPaymentDto(TenantDbContext db, Guid paymentId, CancellationToken ct)
    {
        var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (p == null) return null;
        var pix = await db.PixTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentId == paymentId && x.Active, ct);
        object? pixObj = null;
        if (pix != null)
            pixObj = new { expires_at = pix.ExpiresAt, active = true };
        return new
        {
            p.Id,
            status = p.Status.ToString(),
            method = p.Method?.ToString(),
            amount = MoneyFormatting.Format(p.Amount),
            p.TicketId,
            p.PackageOrderId,
            p.PaidAt,
            p.CreatedAt,
            p.FailedReason,
            pix = pixObj
        };
    }

    public sealed record CreateTicketRequest(string Plate);
    public sealed record CheckoutRequest([property: JsonPropertyName("exit_time")] DateTimeOffset? ExitTime);
}
