using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Application.Validation;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Api.Controllers;

/// <summary>Relatório de saldos: carteira convênio por lojista, bonificação por placa e carteira comprada (gestão).</summary>
[ApiController]
[Route("api/v1/manager/balances-report")]
[Authorize(Roles = $"{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
public sealed class ManagerBalancesReportController(TenantDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? plate, CancellationToken ct)
    {
        string? plateFilter = null;
        if (!string.IsNullOrWhiteSpace(plate))
        {
            var n = PlateValidator.Normalize(plate);
            if (n.Length == 0)
                return BadRequest(new { code = "VALIDATION_ERROR", message = "Filtro de placa inválido." });
            plateFilter = n;
        }

        var now = DateTimeOffset.UtcNow;

        var lojRows = await (
                from l in db.Lojistas.AsNoTracking()
                join w in db.LojistaWallets.AsNoTracking() on l.Id equals w.LojistaId into wj
                from w in wj.DefaultIfEmpty()
                select new { l.Id, l.Name, BalanceHours = w != null ? w.BalanceHours : 0 })
            .ToListAsync(ct);

        var lojistas = lojRows
            .OrderByDescending(x => x.BalanceHours)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new
            {
                lojistaId = x.Id,
                lojistaName = x.Name,
                balanceHours = x.BalanceHours
            })
            .ToList();

        var clientRows = await (
                from c in db.Clients.AsNoTracking()
                join w in db.ClientWallets.AsNoTracking() on c.Id equals w.ClientId into wj
                from w in wj.DefaultIfEmpty()
                select new { c.Plate, Wallet = w })
            .ToListAsync(ct);

        var clientPlates = clientRows
            .Select(x =>
            {
                int bal = 0;
                DateTimeOffset? exp = null;
                if (x.Wallet != null)
                {
                    exp = x.Wallet.ExpirationDate;
                    if (x.Wallet.ExpirationDate == null || x.Wallet.ExpirationDate >= now)
                        bal = x.Wallet.BalanceHours;
                }

                return new { x.Plate, balanceHours = bal, expirationDate = exp };
            })
            .Where(x => plateFilter == null || x.Plate.Contains(plateFilter, StringComparison.Ordinal))
            .OrderByDescending(x => x.balanceHours)
            .ThenBy(x => x.Plate, StringComparer.OrdinalIgnoreCase)
            .Select(x => new
            {
                plate = x.Plate,
                balanceHours = x.balanceHours,
                expirationDate = x.expirationDate
            })
            .ToList();

        var bonificadoRows = await LojistaBonificadoBalance.ListPlatesPositiveBonificadoAsync(db, plateFilter, ct);
        var lojistaBonificadoPlates = bonificadoRows
            .Select(x => new { plate = x.Plate, balanceHours = x.BalanceHours })
            .ToList();

        return Ok(new { lojistas, lojistaBonificadoPlates, clientPlates });
    }
}
