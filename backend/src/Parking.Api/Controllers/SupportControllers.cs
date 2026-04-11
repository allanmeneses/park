using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Application.Validation;
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
    private static bool IsValidScope(string? scope) => scope is "CLIENT" or "LOJISTA";

    private static IQueryable<RechargePackageRow> OrderPackages(IQueryable<RechargePackageRow> query) =>
        query.OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.IsPromo)
            .ThenBy(x => x.Price)
            .ThenBy(x => x.Hours);

    private static object ToDto(RechargePackageRow p) => new
    {
        id = p.Id,
        display_name = p.DisplayName,
        scope = p.Scope,
        hours = p.Hours,
        price = MoneyFormatting.Format(p.Price),
        is_promo = p.IsPromo,
        sort_order = p.SortOrder,
        active = p.Active
    };

    private static IActionResult ValidationError(string message) =>
        new BadRequestObjectResult(new { code = "VALIDATION_ERROR", message });

    private static string NormalizeName(string? value) => (value ?? "").Trim();

    private static string NormalizeScope(string? scope) => (scope ?? "").Trim().ToUpperInvariant();

    private static string? ValidateBody(RechargePackageWrite body)
    {
        if (!IsValidScope(body.Scope))
            return "Scope inválido: use CLIENT ou LOJISTA.";

        if (string.IsNullOrWhiteSpace(NormalizeName(body.DisplayName)))
            return "Nome do pacote é obrigatório.";

        if (NormalizeName(body.DisplayName).Length > 120)
            return "Nome do pacote deve ter no máximo 120 caracteres.";

        if (body.Hours < 1)
            return "Quantidade de horas deve ser maior que zero.";

        if (body.Price < 0)
            return "Preço não pode ser negativo.";

        if (body.SortOrder < 0)
            return "A ordenação deve ser zero ou maior.";

        return null;
    }

    [Authorize(Roles = $"{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)},{nameof(UserRole.CLIENT)},{nameof(UserRole.LOJISTA)}")]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? scope, CancellationToken ct)
    {
        scope = NormalizeScope(scope);
        if (!IsValidScope(scope))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Query scope obrigatória: CLIENT ou LOJISTA." });

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (role == nameof(UserRole.CLIENT) && scope != "CLIENT")
            return StatusCode(403, new { code = "FORBIDDEN", message = "Escopo inválido." });
        if (role == nameof(UserRole.LOJISTA) && scope != "LOJISTA")
            return StatusCode(403, new { code = "FORBIDDEN", message = "Escopo inválido." });

        var items = await OrderPackages(db.RechargePackages.AsNoTracking()
                .Where(p => p.Active && p.Scope == scope))
            .Select(p => new
            {
                id = p.Id,
                display_name = p.DisplayName,
                scope = p.Scope,
                hours = p.Hours,
                price = MoneyFormatting.Format(p.Price),
                is_promo = p.IsPromo,
                sort_order = p.SortOrder,
            })
            .ToListAsync(ct);
        return Ok(new { items });
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpGet("manage")]
    public async Task<IActionResult> Manage([FromQuery] string? scope, CancellationToken ct)
    {
        scope = NormalizeScope(scope);
        if (!IsValidScope(scope))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Query scope obrigatória: CLIENT ou LOJISTA." });

        var items = await OrderPackages(db.RechargePackages.AsNoTracking()
                .Where(p => p.Scope == scope))
            .Select(p => new
            {
                id = p.Id,
                display_name = p.DisplayName,
                scope = p.Scope,
                hours = p.Hours,
                price = MoneyFormatting.Format(p.Price),
                is_promo = p.IsPromo,
                sort_order = p.SortOrder,
                active = p.Active,
            })
            .ToListAsync(ct);

        return Ok(new { items });
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RechargePackageWrite body, CancellationToken ct)
    {
        var error = ValidateBody(body);
        if (error is not null)
            return ValidationError(error);

        var row = new RechargePackageRow
        {
            Id = Guid.NewGuid(),
            DisplayName = NormalizeName(body.DisplayName),
            Scope = NormalizeScope(body.Scope),
            Hours = body.Hours,
            Price = body.Price,
            IsPromo = body.IsPromo,
            SortOrder = body.SortOrder,
            Active = body.Active,
        };

        db.RechargePackages.Add(row);
        await db.SaveChangesAsync(ct);
        return Created($"/api/v1/recharge-packages/{row.Id}", ToDto(row));
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RechargePackageWrite body, CancellationToken ct)
    {
        var error = ValidateBody(body);
        if (error is not null)
            return ValidationError(error);

        var row = await db.RechargePackages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (row == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pacote não encontrado." });

        row.DisplayName = NormalizeName(body.DisplayName);
        row.Scope = NormalizeScope(body.Scope);
        row.Hours = body.Hours;
        row.Price = body.Price;
        row.IsPromo = body.IsPromo;
        row.SortOrder = body.SortOrder;
        row.Active = body.Active;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(row));
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var row = await db.RechargePackages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (row == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pacote não encontrado." });

        var inOrders = await db.PackageOrders.AsNoTracking().AnyAsync(o => o.PackageId == id, ct);
        var inLedger = await db.WalletLedger.AsNoTracking().AnyAsync(w => w.PackageId == id, ct);
        if (inOrders || inLedger)
        {
            return Conflict(new
            {
                code = "PACKAGE_IN_USE",
                message = "Este pacote já foi usado e não pode ser excluído. Desative-o para escondê-lo da lista."
            });
        }

        db.RechargePackages.Remove(row);
        await db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    public sealed record RechargePackageWrite(
        string DisplayName,
        string Scope,
        int Hours,
        decimal Price,
        bool IsPromo,
        int SortOrder,
        bool Active);
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
    private const string RouteGrantClient = "POST /lojista/grant-client";

    private Guid ParkingId => (Guid)http.HttpContext!.Items[ParkingConstants.ParkingIdItem]!;
    private Guid EntityId => Guid.Parse(User.FindFirst("entity_id")!.Value!);

    [HttpGet("grant-settings")]
    public async Task<IActionResult> GetGrantSettings(CancellationToken ct)
    {
        var loj = await db.Lojistas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == EntityId, ct);
        if (loj is null)
            return StatusCode(500, new { code = "INTERNAL", message = "Lojista não encontrado no tenant." });
        return Ok(new Dictionary<string, bool> { ["allow_grant_before_entry"] = loj.AllowGrantBeforeEntry });
    }

    [HttpPut("grant-settings")]
    public async Task<IActionResult> PutGrantSettings([FromBody] GrantSettingsBody body, CancellationToken ct)
    {
        var loj = await db.Lojistas.FirstAsync(x => x.Id == EntityId, ct);
        loj.AllowGrantBeforeEntry = body.AllowGrantBeforeEntry;
        await db.SaveChangesAsync(ct);
        return Ok(new Dictionary<string, bool> { ["allow_grant_before_entry"] = loj.AllowGrantBeforeEntry });
    }

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

    /// <summary>Bonifica horas da carteira do lojista para a carteira do cliente (placa ou ticket).</summary>
    [HttpPost("grant-client")]
    public async Task<IActionResult> GrantClient(
        [FromHeader(Name = "Idempotency-Key")] string? idemKey,
        [FromBody] GrantClientBody? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idemKey))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Idempotency-Key obrigatório." });
        if (body is null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Corpo JSON obrigatório." });

        var hours = body.Hours is null or < 1 ? 1 : body.Hours.Value;
        if (hours > 720)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Quantidade de horas inválida (máx. 720)." });

        TicketRow? ticketRow = null;
        string? plateNorm = null;
        if (body.TicketId is Guid tid)
        {
            ticketRow = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tid, ct);
            if (ticketRow is null)
                return NotFound(new { code = "NOT_FOUND", message = "Ticket não encontrado." });
            plateNorm = ticketRow.Plate;
        }
        else if (!string.IsNullOrWhiteSpace(body.Plate))
        {
            plateNorm = PlateValidator.Normalize(body.Plate);
            if (!PlateValidator.IsValidNormalized(plateNorm))
                return BadRequest(new { code = "PLATE_INVALID", message = "Placa inválida." });
        }
        else
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Informe placa ou ticketId." });

        var lojRules = await db.Lojistas.AsNoTracking().FirstAsync(x => x.Id == EntityId, ct);
        var grantMode = "ADVANCE";
        if (!lojRules.AllowGrantBeforeEntry)
        {
            if (ticketRow is not null)
            {
                if (ticketRow.Status is not (TicketStatus.OPEN or TicketStatus.AWAITING_PAYMENT))
                {
                    return Conflict(new
                    {
                        code = "GRANT_REQUIRES_ACTIVE_TICKET",
                        message = "Este ticket não está em aberto. Ajuste as preferências para permitir crédito antecipado ou use um veículo no estacionamento.",
                    });
                }

                grantMode = "ON_SITE";
            }
            else if (!await db.Tickets.AsNoTracking().AnyAsync(
                         x => x.Plate == plateNorm &&
                              (x.Status == TicketStatus.OPEN || x.Status == TicketStatus.AWAITING_PAYMENT),
                         ct))
            {
                return Conflict(new
                {
                    code = "GRANT_REQUIRES_ACTIVE_TICKET",
                    message = "Não há veículo no estacionamento com ticket em aberto para esta placa. Altere as preferências para permitir crédito antecipado.",
                });
            }
            else
            {
                grantMode = "ON_SITE";
            }
        }
        else if (ticketRow is not null)
        {
            grantMode = "ON_SITE";
        }

        var cached = await db.IdempotencyStore.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == idemKey && x.Route == RouteGrantClient, ct);
        if (cached is not null)
            return Content(cached.ResponseJson, "application/json");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var lw = await db.LojistaWallets.FirstOrDefaultAsync(x => x.LojistaId == EntityId, ct);
            if (lw is null || lw.BalanceHours < hours)
            {
                await tx.RollbackAsync(ct);
                return Conflict(new
                {
                    code = "LOJISTA_CREDIT_INSUFFICIENT",
                    message = "Créditos insuficientes na sua carteira de convênio.",
                });
            }

            var client = await db.Clients.FirstOrDefaultAsync(x => x.Plate == plateNorm, ct);
            if (client is null)
            {
                client = new ClientRow { Id = Guid.NewGuid(), Plate = plateNorm!, LojistaId = EntityId };
                db.Clients.Add(client);
                db.ClientWallets.Add(new ClientWalletRow
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    BalanceHours = 0,
                    ExpirationDate = null,
                });
                await db.SaveChangesAsync(ct);
            }
            else
            {
                if (client.LojistaId is { } other && other != EntityId)
                {
                    await tx.RollbackAsync(ct);
                    return Conflict(new
                    {
                        code = "CLIENT_FOR_OTHER_LOJISTA",
                        message = "Esta placa está vinculada a outro convênio.",
                    });
                }

                if (client.LojistaId is null)
                    client.LojistaId = EntityId;
            }

            var grantedTotalBefore = await LojistaBonificadoBalance.SumGrantedHoursAsync(db, EntityId, plateNorm!, ct);
            var usedTotal = await LojistaBonificadoBalance.SumGrantScopedLojistaUsedHoursAsync(db, EntityId, plateNorm!, ct);

            lw.BalanceHours -= hours;
            var grantId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            db.LojistaGrants.Add(new LojistaGrantRow
            {
                Id = grantId,
                LojistaId = EntityId,
                ClientId = client.Id,
                Plate = plateNorm!,
                Hours = hours,
                GrantMode = grantMode,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(ct);

            await audit.AppendAsync(ParkingId, "grant", grantId, "LOJISTA_GRANT_CLIENT",
                new { grant_id = grantId, plate = plateNorm, hours, client_id = client.Id }, ct);

            var grantedBalance = LojistaBonificadoBalance.Available(grantedTotalBefore + hours, usedTotal);

            var payload = JsonSerializer.Serialize(new
            {
                grant_id = grantId,
                plate = plateNorm,
                hours,
                grant_mode = grantMode,
                client_balance_hours = grantedBalance,
                lojista_balance_hours = lw.BalanceHours,
            });
            db.IdempotencyStore.Add(new IdempotencyStoreRow
            {
                Key = idemKey,
                Route = RouteGrantClient,
                ResponseJson = payload,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Ok(JsonSerializer.Deserialize<JsonElement>(payload));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Extrato de bonificações com filtros opcionais (UTC).</summary>
    [HttpGet("grant-client/history")]
    public async Task<IActionResult> GrantHistory(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? plate,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var lim = Math.Clamp(limit ?? 100, 1, 200);
        var q = db.LojistaGrants.AsNoTracking().Where(x => x.LojistaId == EntityId);
        if (from is { } f)
            q = q.Where(x => x.CreatedAt >= f);
        if (to is { } t)
            q = q.Where(x => x.CreatedAt <= t);
        if (!string.IsNullOrWhiteSpace(plate))
        {
            var p = PlateValidator.Normalize(plate);
            if (PlateValidator.IsValidNormalized(p))
                q = q.Where(x => x.Plate == p);
        }

        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(lim)
            .Select(x => new
            {
                id = x.Id,
                created_at = x.CreatedAt,
                plate = x.Plate,
                hours = x.Hours,
                grant_mode = x.GrantMode,
                client_id = x.ClientId,
            })
            .ToListAsync(ct);

        return Ok(new { items });
    }

    public sealed record LojistaBuy(Guid PackageId, string Settlement);

    public sealed record GrantClientBody(string? Plate, Guid? TicketId, int? Hours);

    public sealed class GrantSettingsBody
    {
        [JsonPropertyName("allow_grant_before_entry")]
        public bool AllowGrantBeforeEntry { get; set; }
    }
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
        [FromQuery(Name = "lojista_id")] Guid? lojistaId = null,
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
                Method = p.Method != null ? p.Method.ToString() : null,
                TicketId = p.TicketId,
                PackageOrderId = p.PackageOrderId
            })
            .ToListAsync(ct);

        var ticketIds = paymentRows.Where(x => x.TicketId != null).Select(x => x.TicketId!.Value).Distinct().ToList();
        var ticketUsageByTicket = await db.WalletUsages.AsNoTracking()
            .Where(u => ticketIds.Contains(u.TicketId))
            .GroupBy(u => u.TicketId)
            .Select(g => new
            {
                TicketId = g.Key,
                LojistaHours = g.Where(x => x.Source == "lojista").Sum(x => x.HoursUsed),
                ClientHours = g.Where(x => x.Source == "client").Sum(x => x.HoursUsed),
            })
            .ToListAsync(ct);
        var usageMap = ticketUsageByTicket.ToDictionary(x => x.TicketId, x => x);

        var ticketInfoMap = await (
            from t in db.Tickets.AsNoTracking()
            join c in db.Clients.AsNoTracking() on t.Plate equals c.Plate into cgj
            from c in cgj.DefaultIfEmpty()
            where ticketIds.Contains(t.Id)
            select new
            {
                t.Id,
                LojistaId = c != null ? c.LojistaId : null,
                t.EntryTime,
                t.ExitTime
            }
        ).ToDictionaryAsync(
            x => x.Id,
            x =>
            {
                var exit = x.ExitTime ?? x.EntryTime;
                var hours = (int)Math.Ceiling(Math.Max(0, (exit - x.EntryTime).TotalHours));
                return new { x.LojistaId, HoursTotal = hours };
            },
            ct);

        var packageOrderIds = paymentRows.Where(p => p.PackageOrderId != null).Select(p => p.PackageOrderId!.Value).Distinct().ToList();
        var packageLojistaMap = await db.PackageOrders.AsNoTracking()
            .Where(o => o.LojistaId != null && packageOrderIds.Contains(o.Id))
            .ToDictionaryAsync(x => x.Id, x => x.LojistaId, ct);

        var usageRows = await (
            from u in db.WalletUsages.AsNoTracking()
            join t in db.Tickets.AsNoTracking() on u.TicketId equals t.Id
            join c in db.Clients.AsNoTracking() on t.Plate equals c.Plate into cgj
            from c in cgj.DefaultIfEmpty()
            where t.ExitTime != null && t.ExitTime.Value >= rangeStart && t.ExitTime.Value <= rangeEnd
                  && (lojistaId == null || c != null && c.LojistaId == lojistaId)
            select new
            {
                At = t.ExitTime!.Value,
                Kind = u.Source == "lojista" ? "LOJISTA_USAGE" : "CLIENT_USAGE",
                Amount = 0m,
                Ref = u.Id,
                Method = (string?)null,
                LojistaId = c != null ? c.LojistaId : null
            }).ToListAsync(ct);

        var paymentItems = paymentRows
            .Where(x =>
            {
                if (lojistaId == null) return true;
                if (x.TicketId is { } tid && ticketInfoMap.TryGetValue(tid, out var tInfo)) return tInfo.LojistaId == lojistaId;
                if (x.PackageOrderId is { } oid && packageLojistaMap.TryGetValue(oid, out var pLoj)) return pLoj == lojistaId;
                return false;
            })
            .Select(x =>
            {
                int hLoj = 0;
                int hCli = 0;
                int hCash = 0;
                string? splitType = null;
                Guid? itemLojistaId = null;
                if (x.TicketId is { } tid)
                {
                    if (usageMap.TryGetValue(tid, out var us))
                    {
                        hLoj = us.LojistaHours;
                        hCli = us.ClientHours;
                    }

                    if (ticketInfoMap.TryGetValue(tid, out var tInfo))
                    {
                        itemLojistaId = tInfo.LojistaId;
                        hCash = Math.Max(0, tInfo.HoursTotal - hLoj - hCli);
                    }

                    if (hLoj > 0 && hCli > 0) splitType = "MIXED";
                    else if (hLoj > 0 && hCli == 0 && x.Amount == 0) splitType = "LOJISTA_ONLY";
                    else if (hLoj == 0 && hCli > 0 && x.Amount == 0) splitType = "CLIENT_WALLET_ONLY";
                    else if (hLoj > 0 && x.Amount > 0) splitType = "MIXED";
                    else splitType = "CLIENT_DIRECT_ONLY";

                }
                else if (x.PackageOrderId is { } oid && packageLojistaMap.TryGetValue(oid, out var pLoj))
                {
                    itemLojistaId = pLoj;
                }

                return new MovementItem(
                    x.At,
                    x.Kind,
                    MoneyFormatting.Format(x.Amount),
                    x.Ref,
                    x.Method,
                    itemLojistaId,
                    splitType,
                    hLoj,
                    hCli,
                    hCash);
            });

        var all = paymentItems
            .Concat(usageRows.Select(x => new MovementItem(x.At, x.Kind, MoneyFormatting.Format(x.Amount), x.Ref, x.Method, x.LojistaId)))
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

    public sealed record MovementItem(
        DateTimeOffset At,
        string Kind,
        string Amount,
        Guid Ref,
        string? Method,
        Guid? LojistaId = null,
        string? TicketSplitType = null,
        int HoursLojista = 0,
        int HoursCliente = 0,
        int HoursDirect = 0);
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
