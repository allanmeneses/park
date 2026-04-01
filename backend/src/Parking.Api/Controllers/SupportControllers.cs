using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Domain;
using Parking.Infrastructure.Audit;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Api.Parking;

namespace Parking.Api.Controllers;

file static class TenantSettingsGuard
{
    private static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000000");

    /// <summary>SPEC §21 — singleton de settings; SQL idempotente evita corrida entre provisionamento e primeiro GET.</summary>
    internal static async Task EnsureAsync(TenantDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO settings (id, price_per_hour, capacity)
            VALUES ('00000000-0000-0000-0000-000000000000'::uuid, 5.00, 50)
            ON CONFLICT (id) DO NOTHING
            """,
            cancellationToken: ct);
    }

    internal static Guid Singleton => SingletonId;
}

[ApiController]
[Route("api/v1/settings")]
[Authorize(Roles = $"{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
public sealed class SettingsController(TenantDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        await TenantSettingsGuard.EnsureAsync(db, ct);
        var s = await db.Settings.AsNoTracking().FirstAsync(x => x.Id == TenantSettingsGuard.Singleton, ct);
        return Ok(new { price_per_hour = MoneyFormatting.Format(s.PricePerHour), capacity = s.Capacity });
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] SettingsPost body, CancellationToken ct)
    {
        await TenantSettingsGuard.EnsureAsync(db, ct);
        var s = await db.Settings.FirstAsync(x => x.Id == TenantSettingsGuard.Singleton, ct);
        s.PricePerHour = body.PricePerHour;
        s.Capacity = body.Capacity;
        await db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    public sealed record SettingsPost(decimal PricePerHour, int Capacity);
}

[ApiController]
[Route("api/v1/recharge-packages")]
public sealed class RechargePackagesController(TenantDbContext db) : ControllerBase
{
    [Authorize(Roles = $"{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)},{nameof(UserRole.CLIENT)},{nameof(UserRole.LOJISTA)}")]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? scope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(scope) || (scope != "CLIENT" && scope != "LOJISTA"))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Query scope obrigatória: CLIENT ou LOJISTA." });

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (role == nameof(UserRole.CLIENT) && scope != "CLIENT")
            return StatusCode(403, new { code = "FORBIDDEN", message = "Escopo inválido." });
        if (role == nameof(UserRole.LOJISTA) && scope != "LOJISTA")
            return StatusCode(403, new { code = "FORBIDDEN", message = "Escopo inválido." });

        var items = await db.RechargePackages.AsNoTracking()
            .Where(p => p.Active && p.Scope == scope)
            .Select(p => new { p.Id, p.Scope, p.Hours, price = MoneyFormatting.Format(p.Price) })
            .ToListAsync(ct);
        return Ok(new { items });
    }
}

[ApiController]
[Route("api/v1/client")]
[Authorize(Roles = nameof(UserRole.CLIENT))]
public sealed class ClientController(
    TenantDbContext db,
    AuditService audit,
    IHttpContextAccessor http) : ControllerBase
{
    private Guid ParkingId => (Guid)http.HttpContext!.Items[ParkingConstants.ParkingIdItem]!;
    private Guid EntityId => Guid.Parse(User.FindFirst("entity_id")!.Value!);

    [HttpGet("wallet")]
    public async Task<IActionResult> Wallet(CancellationToken ct)
    {
        var w = await db.ClientWallets.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == EntityId, ct);
        if (w == null)
            return Ok(new { balance_hours = 0, expiration_date = (DateTimeOffset?)null });
        return Ok(new { balance_hours = w.BalanceHours, expiration_date = w.ExpirationDate });
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int limit = 50, [FromQuery] string? cursor = null, CancellationToken ct = default)
    {
        var body = await WalletHistoryBuilder.BuildClientHistoryAsync(db, EntityId, limit, cursor, ct);
        return Ok(body);
    }

    [HttpPost("buy")]
    public async Task<IActionResult> Buy([FromHeader(Name = "Idempotency-Key")] string? idem, [FromBody] ClientBuy body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idem))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Idempotency-Key obrigatório." });
        var pkg = await db.RechargePackages.FirstOrDefaultAsync(p => p.Id == body.PackageId && p.Active && p.Scope == "CLIENT", ct);
        if (pkg == null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Pacote inválido." });

        if (body.Settlement == "CREDIT")
        {
            var orderId = Guid.NewGuid();
            db.PackageOrders.Add(new PackageOrderRow
            {
                Id = orderId,
                Scope = "CLIENT",
                ClientId = EntityId,
                LojistaId = null,
                PackageId = pkg.Id,
                Status = "PAID",
                Settlement = "CREDIT",
                Amount = pkg.Price,
                CreatedAt = DateTimeOffset.UtcNow,
                PaidAt = DateTimeOffset.UtcNow
            });
            var w = await db.ClientWallets.FirstOrDefaultAsync(x => x.ClientId == EntityId, ct);
            if (w == null)
            {
                w = new ClientWalletRow { Id = Guid.NewGuid(), ClientId = EntityId, BalanceHours = 0, ExpirationDate = null };
                db.ClientWallets.Add(w);
            }

            w.BalanceHours += pkg.Hours;
            await db.SaveChangesAsync(ct);
            await audit.AppendAsync(ParkingId, "package", orderId, "PACKAGE_PURCHASE",
                new { order_id = orderId, package_id = pkg.Id, settlement = "CREDIT" }, ct);
            return Ok(new { order_id = orderId, status = "PAID", balance_hours = w.BalanceHours });
        }

        var payId = Guid.NewGuid();
        var oid = Guid.NewGuid();
        db.PackageOrders.Add(new PackageOrderRow
        {
            Id = oid,
            Scope = "CLIENT",
            ClientId = EntityId,
            LojistaId = null,
            PackageId = pkg.Id,
            Status = "AWAITING_PAYMENT",
            Settlement = "PIX",
            Amount = pkg.Price,
            CreatedAt = DateTimeOffset.UtcNow,
            PaidAt = null
        });
        db.Payments.Add(new PaymentRow
        {
            Id = payId,
            TicketId = null,
            PackageOrderId = oid,
            Method = null,
            Status = PaymentStatus.PENDING,
            Amount = pkg.Price,
            TransactionId = null,
            IdempotencyKey = idem,
            CreatedAt = DateTimeOffset.UtcNow,
            PaidAt = null,
            FailedReason = null
        });
        await db.SaveChangesAsync(ct);
        return Ok(new { order_id = oid, payment_id = payId, status = "AWAITING_PAYMENT" });
    }

    public sealed record ClientBuy(Guid PackageId, string Settlement);
}

[ApiController]
[Route("api/v1/lojista")]
[Authorize(Roles = nameof(UserRole.LOJISTA))]
public sealed class LojistaController(TenantDbContext db, AuditService audit, IHttpContextAccessor http) : ControllerBase
{
    private Guid ParkingId => (Guid)http.HttpContext!.Items[ParkingConstants.ParkingIdItem]!;
    private Guid EntityId => Guid.Parse(User.FindFirst("entity_id")!.Value!);

    [HttpGet("wallet")]
    public async Task<IActionResult> Wallet(CancellationToken ct)
    {
        var w = await db.LojistaWallets.AsNoTracking().FirstOrDefaultAsync(x => x.LojistaId == EntityId, ct);
        if (w == null)
            return Ok(new { balance_hours = 0 });
        return Ok(new { balance_hours = w.BalanceHours });
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int limit = 50, [FromQuery] string? cursor = null, CancellationToken ct = default)
    {
        var body = await WalletHistoryBuilder.BuildLojistaHistoryAsync(db, EntityId, limit, cursor, ct);
        return Ok(body);
    }

    [HttpPost("buy")]
    public async Task<IActionResult> Buy([FromHeader(Name = "Idempotency-Key")] string? idem, [FromBody] LojistaBuy body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idem))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Idempotency-Key obrigatório." });
        var pkg = await db.RechargePackages.FirstOrDefaultAsync(p => p.Id == body.PackageId && p.Active && p.Scope == "LOJISTA", ct);
        if (pkg == null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Pacote inválido." });

        if (body.Settlement == "CREDIT")
        {
            var orderId = Guid.NewGuid();
            db.PackageOrders.Add(new PackageOrderRow
            {
                Id = orderId,
                Scope = "LOJISTA",
                ClientId = null,
                LojistaId = EntityId,
                PackageId = pkg.Id,
                Status = "PAID",
                Settlement = "CREDIT",
                Amount = pkg.Price,
                CreatedAt = DateTimeOffset.UtcNow,
                PaidAt = DateTimeOffset.UtcNow
            });
            var w = await db.LojistaWallets.FirstOrDefaultAsync(x => x.LojistaId == EntityId, ct);
            if (w == null)
            {
                w = new LojistaWalletRow { Id = Guid.NewGuid(), LojistaId = EntityId, BalanceHours = 0 };
                db.LojistaWallets.Add(w);
            }

            w.BalanceHours += pkg.Hours;
            await db.SaveChangesAsync(ct);
            await audit.AppendAsync(ParkingId, "package", orderId, "PACKAGE_PURCHASE",
                new { order_id = orderId, package_id = pkg.Id, settlement = "CREDIT" }, ct);
            return Ok(new { order_id = orderId, status = "PAID", balance_hours = w.BalanceHours });
        }

        var payId = Guid.NewGuid();
        var oid = Guid.NewGuid();
        db.PackageOrders.Add(new PackageOrderRow
        {
            Id = oid,
            Scope = "LOJISTA",
            ClientId = null,
            LojistaId = EntityId,
            PackageId = pkg.Id,
            Status = "AWAITING_PAYMENT",
            Settlement = "PIX",
            Amount = pkg.Price,
            CreatedAt = DateTimeOffset.UtcNow,
            PaidAt = null
        });
        db.Payments.Add(new PaymentRow
        {
            Id = payId,
            TicketId = null,
            PackageOrderId = oid,
            Method = null,
            Status = PaymentStatus.PENDING,
            Amount = pkg.Price,
            TransactionId = null,
            IdempotencyKey = idem,
            CreatedAt = DateTimeOffset.UtcNow,
            PaidAt = null,
            FailedReason = null
        });
        await db.SaveChangesAsync(ct);
        return Ok(new { order_id = oid, payment_id = payId, status = "AWAITING_PAYMENT" });
    }

    public sealed record LojistaBuy(Guid PackageId, string Settlement);
}

[ApiController]
[Route("api/v1/cash")]
[Authorize(Roles = $"{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
public sealed class CashController(TenantDbContext db, AuditService audit, IHttpContextAccessor http) : ControllerBase
{
    private Guid ParkingId => (Guid)http.HttpContext!.Items[ParkingConstants.ParkingIdItem]!;

    [HttpPost("open")]
    public async Task<IActionResult> Open(CancellationToken ct)
    {
        if (await db.CashSessions.AnyAsync(s => s.Status == CashSessionStatus.OPEN, ct))
            return Conflict(new { code = "CONFLICT", message = "Caixa já aberto." });
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.CashSessions.Add(new CashSessionRow
        {
            Id = id,
            Status = CashSessionStatus.OPEN,
            OpenedAt = now,
            ClosedAt = null,
            ExpectedAmount = 0,
            ActualAmount = null
        });
        await db.SaveChangesAsync(ct);
        await audit.AppendAsync(ParkingId, "cash", id, "CASH_OPEN", new { session_id = id, expected_amount = 0m, actual_amount = (decimal?)null }, ct);
        return Ok(new { session_id = id, opened_at = now });
    }

    [HttpPost("close")]
    public async Task<IActionResult> Close([FromBody] CashClose body, CancellationToken ct)
    {
        var s = await db.CashSessions.FirstOrDefaultAsync(x => x.Id == body.SessionId && x.Status == CashSessionStatus.OPEN, ct);
        if (s == null)
            return NotFound(new { code = "NOT_FOUND", message = "Sessão não encontrada." });
        s.Status = CashSessionStatus.CLOSED;
        s.ClosedAt = DateTimeOffset.UtcNow;
        s.ActualAmount = body.ActualAmount;
        var expected = s.ExpectedAmount;
        var actual = body.ActualAmount;
        var div = expected == 0 ? 0 : Math.Abs(actual - expected) / expected;
        var alert = div > 0.05m && expected != 0;
        if (alert)
        {
            db.Alerts.Add(new AlertRow
            {
                Id = Guid.NewGuid(),
                Type = "CASH_DIVERGENCE",
                Payload = JsonSerializer.Serialize(new
                {
                    session_id = s.Id,
                    expected_amount = expected,
                    actual_amount = actual,
                    divergence_ratio = (double)div
                }),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        await audit.AppendAsync(ParkingId, "cash", s.Id, "CASH_CLOSE", new { session_id = s.Id, expected_amount = expected, actual_amount = actual }, ct);
        return Ok(new
        {
            session_id = s.Id,
            expected_amount = MoneyFormatting.Format(expected),
            actual_amount = MoneyFormatting.Format(actual),
            divergence = (double)div,
            alert
        });
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var open = await db.CashSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Status == CashSessionStatus.OPEN, ct);
        var last = await db.CashSessions.AsNoTracking()
            .Where(s => s.Status == CashSessionStatus.CLOSED)
            .OrderByDescending(s => s.ClosedAt)
            .FirstOrDefaultAsync(ct);
        object? openObj = open == null
            ? null
            : new { session_id = open.Id, opened_at = open.OpenedAt, expected_amount = MoneyFormatting.Format(open.ExpectedAmount) };
        object? lastObj = last == null
            ? null
            : new
            {
                session_id = last.Id,
                expected_amount = MoneyFormatting.Format(last.ExpectedAmount),
                actual_amount = last.ActualAmount is { } aa ? MoneyFormatting.Format(aa) : null,
            };
        return Ok(new { open = openObj, last_closed = lastObj });
    }

    public sealed record CashClose(Guid SessionId, decimal ActualAmount);
}

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Roles = $"{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
public sealed class DashboardController(TenantDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? view = null, CancellationToken ct = default)
    {
        await TenantSettingsGuard.EnsureAsync(db, ct);
        var nowUtc = DateTimeOffset.UtcNow;
        var mode = string.Equals(view, "24h", StringComparison.OrdinalIgnoreCase) ? "24h" : "today";
        var startUtc = mode == "24h"
            ? nowUtc.AddHours(-24)
            : new DateTimeOffset(nowUtc.UtcDateTime.Date, TimeSpan.Zero);
        var endUtc = nowUtc;

        var paidToday = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.PAID && p.PaidAt != null && p.PaidAt.Value >= startUtc && p.PaidAt.Value <= endUtc)
            .SumAsync(p => p.Amount, ct);
        var cap = await db.Settings.AsNoTracking().Select(s => s.Capacity).FirstAsync(ct);
        var openCount = await db.Tickets.CountAsync(t => t.Status == TicketStatus.OPEN, ct);
        var ocup = cap == 0 ? 0 : (double)openCount / cap;
        var ticketsDia = await db.Tickets.CountAsync(
            t => t.ExitTime != null && t.ExitTime.Value >= startUtc && t.ExitTime.Value <= endUtc, ct);

        var numerador = await (
            from u in db.WalletUsages.AsNoTracking()
            join t in db.Tickets.AsNoTracking() on u.TicketId equals t.Id
            join p in db.Payments.AsNoTracking() on t.Id equals p.TicketId
            where u.Source == "lojista"
                  && p.Status == PaymentStatus.PAID
                  && p.PaidAt != null
                  && p.PaidAt.Value >= startUtc
                  && p.PaidAt.Value <= endUtc
            select t.Id).Distinct().CountAsync(ct);
        var denom = await db.Tickets.CountAsync(
            t => t.Status == TicketStatus.CLOSED
                 && t.ExitTime != null
                 && t.ExitTime.Value >= startUtc
                 && t.ExitTime.Value <= endUtc, ct);
        double? usoConvenio = denom == 0 ? null : (double)numerador / denom;

        if (usoConvenio is > 0.2)
        {
            var already = await db.Alerts.AsNoTracking().AnyAsync(
                a => a.Type == "CONVENIO_RATIO" && a.CreatedAt >= startUtc && a.CreatedAt <= endUtc, ct);
            if (!already)
            {
                db.Alerts.Add(new AlertRow
                {
                    Id = Guid.NewGuid(),
                    Type = "CONVENIO_RATIO",
                    Payload = JsonSerializer.Serialize(new { ratio = usoConvenio, from = startUtc, to = endUtc }),
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync(ct);
            }
        }

        return Ok(new
        {
            faturamento = (double)paidToday,
            ocupacao = ocup,
            tickets_dia = ticketsDia,
            uso_convenio = usoConvenio,
            view = mode
        });
    }
}

[ApiController]
[Route("api/v1/operator")]
[Authorize(Roles = $"{nameof(UserRole.OPERATOR)},{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
public sealed class OperatorController(TenantDbContext db) : ControllerBase
{
    [HttpPost("problem")]
    public async Task<IActionResult> Problem(CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(sub))
            return Unauthorized();
        var uid = Guid.Parse(sub);
        db.OperatorEvents.Add(new OperatorEventRow
        {
            Id = Guid.NewGuid(),
            UserId = uid,
            Type = "PROBLEM",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }
}

[ApiController]
[Route("api/v1/manager/movements")]
[Authorize(Roles = $"{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
public sealed class ManagerMovementsController(TenantDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? kind = null,
        [FromQuery] int limit = 200,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var rangeEnd = to ?? now;
        var rangeStart = from ?? rangeEnd.AddDays(-7);
        if (rangeEnd < rangeStart)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Intervalo inválido: 'to' menor que 'from'." });

        limit = Math.Clamp(limit, 1, 500);

        var paymentRows = await db.Payments.AsNoTracking()
            .Where(p => p.PaidAt != null && p.PaidAt.Value >= rangeStart && p.PaidAt.Value <= rangeEnd)
            .Select(p => new
            {
                At = p.PaidAt!.Value,
                Kind = p.TicketId != null ? "TICKET_PAYMENT" : "PACKAGE_PAYMENT",
                Amount = p.Amount,
                Ref = p.Id,
                Method = p.Method != null ? p.Method.ToString() : null
            })
            .ToListAsync(ct);

        var usageRows = await (
            from u in db.WalletUsages.AsNoTracking()
            join t in db.Tickets.AsNoTracking() on u.TicketId equals t.Id
            where t.ExitTime != null && t.ExitTime.Value >= rangeStart && t.ExitTime.Value <= rangeEnd
            select new
            {
                At = t.ExitTime!.Value,
                Kind = u.Source == "lojista" ? "LOJISTA_USAGE" : "CLIENT_USAGE",
                Amount = 0m,
                Ref = u.Id,
                Method = (string?)null
            }).ToListAsync(ct);

        var all = paymentRows
            .Select(x => new MovementItem(x.At, x.Kind, MoneyFormatting.Format(x.Amount), x.Ref, x.Method))
            .Concat(usageRows.Select(x => new MovementItem(x.At, x.Kind, MoneyFormatting.Format(x.Amount), x.Ref, x.Method)))
            .OrderByDescending(x => x.At)
            .ToList();

        if (!string.IsNullOrWhiteSpace(kind))
            all = all.Where(x => x.Kind.Equals(kind.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        var sliced = all.Take(limit).ToList();

        var totalTicket = paymentRows.Where(x => x.Kind == "TICKET_PAYMENT").Sum(x => x.Amount);
        var totalPackage = paymentRows.Where(x => x.Kind == "PACKAGE_PAYMENT").Sum(x => x.Amount);
        var lojistaUsage = usageRows.Count(x => x.Kind == "LOJISTA_USAGE");
        var clientUsage = usageRows.Count(x => x.Kind == "CLIENT_USAGE");

        return Ok(new
        {
            from = rangeStart,
            to = rangeEnd,
            count = sliced.Count,
            insights = new
            {
                total_ticket = MoneyFormatting.Format(totalTicket),
                total_package = MoneyFormatting.Format(totalPackage),
                usages_lojista = lojistaUsage,
                usages_client = clientUsage
            },
            items = sliced
        });
    }

    public sealed record MovementItem(DateTimeOffset At, string Kind, string Amount, Guid Ref, string? Method);
}

[ApiController]
[Route("api/v1/manager/analytics")]
[Authorize(Roles = $"{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
public sealed class ManagerAnalyticsController(TenantDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int days = 14, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 90);
        var now = DateTimeOffset.UtcNow;
        var from = now.AddDays(-days);

        var paid = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.PAID && p.PaidAt != null && p.PaidAt.Value >= from && p.PaidAt.Value <= now)
            .Select(p => new { p.Amount, At = p.PaidAt!.Value })
            .ToListAsync(ct);

        var checkouts = await db.Tickets.AsNoTracking()
            .Where(t => t.ExitTime != null && t.ExitTime.Value >= from && t.ExitTime.Value <= now)
            .Select(t => t.ExitTime!.Value)
            .ToListAsync(ct);

        var trend = paid
            .GroupBy(x => x.At.UtcDateTime.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                day = g.Key.ToString("yyyy-MM-dd"),
                amount = MoneyFormatting.Format(g.Sum(x => x.Amount)),
                payments = g.Count()
            })
            .ToList();

        var gainsByHour = paid
            .GroupBy(x => x.At.UtcDateTime.Hour)
            .OrderBy(g => g.Key)
            .Select(g => new { hour = g.Key, amount = MoneyFormatting.Format(g.Sum(x => x.Amount)), payments = g.Count() })
            .ToList();

        var peakHours = checkouts
            .GroupBy(x => x.UtcDateTime.Hour)
            .Select(g => new { hour = g.Key, checkouts = g.Count() })
            .OrderByDescending(x => x.checkouts)
            .ThenBy(x => x.hour)
            .Take(3)
            .ToList();

        return Ok(new
        {
            from,
            to = now,
            days,
            totals = new
            {
                revenue = MoneyFormatting.Format(paid.Sum(x => x.Amount)),
                payments = paid.Count,
                checkouts = checkouts.Count
            },
            trend_by_day = trend,
            gains_by_hour = gainsByHour,
            peak_hours = peakHours
        });
    }
}
