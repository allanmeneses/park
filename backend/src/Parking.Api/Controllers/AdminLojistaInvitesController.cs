using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Application.Lojistas;
using Parking.Domain;
using Parking.Api.Parking;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Api.Controllers;

[ApiController]
[Route("api/v1/admin/lojista-invites")]
public sealed class AdminLojistaInvitesController(
    IdentityDbContext identity,
    TenantDbContext tenant) : ControllerBase
{
    /// <summary>Cria lojista no tenant, carteira zerada e convite com códigos.</summary>
    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLojistaInviteRequest? body, CancellationToken ct)
    {
        var parkingId = (Guid?)HttpContext.Items[ParkingConstants.ParkingIdItem];
        if (parkingId is null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Tenant não resolvido." });

        var displayName = string.IsNullOrWhiteSpace(body?.DisplayName)
            ? "Lojista pendente"
            : body!.DisplayName.Trim();

        const int maxAttempts = 32;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var merchantCode = LojistaInviteCodes.GenerateMerchantCode();
            var activationClear = LojistaInviteCodes.GenerateActivationCode();
            var activationHash = LojistaInviteCodes.HashActivationCode(activationClear);

            var lojistaId = Guid.NewGuid();

            try
            {
                tenant.Lojistas.Add(new LojistaRow
                {
                    Id = lojistaId,
                    Name = displayName,
                    HourPrice = 0m,
                });
                tenant.LojistaWallets.Add(new LojistaWalletRow
                {
                    Id = Guid.NewGuid(),
                    LojistaId = lojistaId,
                    BalanceHours = 0,
                });
                await tenant.SaveChangesAsync(ct);

                identity.LojistaInvites.Add(new LojistaInviteRow
                {
                    Id = Guid.NewGuid(),
                    ParkingId = parkingId.Value,
                    LojistaId = lojistaId,
                    MerchantCode = merchantCode,
                    ActivationCodeHash = activationHash,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await identity.SaveChangesAsync(ct);

                return StatusCode((int)HttpStatusCode.Created, new
                {
                    merchantCode = merchantCode,
                    activationCode = activationClear,
                    lojistaId,
                });
            }
            catch (DbUpdateException)
            {
                var tracked = await tenant.Lojistas.FindAsync([lojistaId], ct);
                if (tracked is not null)
                    tenant.Lojistas.Remove(tracked);
                var w = await tenant.LojistaWallets.FirstOrDefaultAsync(x => x.LojistaId == lojistaId, ct);
                if (w is not null)
                    tenant.LojistaWallets.Remove(w);
                if (tracked is not null || w is not null)
                    await tenant.SaveChangesAsync(ct);
                // colisão em merchant_code (improvável) ou falha identity após tenant
            }
        }

        return StatusCode(500, new { code = "INTERNAL", message = "Não foi possível gerar código único." });
    }

    /// <summary>
    /// Lista todos os lojistas do tenant (tabela <c>lojistas</c>), enriquecido com convite (se existir),
    /// e-mail da conta quando ativada, horas compradas (soma de pacotes pagos) e saldo atual.
    /// </summary>
    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var parkingId = (Guid?)HttpContext.Items[ParkingConstants.ParkingIdItem];
        if (parkingId is null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Tenant não resolvido." });

        var inviteByLojista = await identity.LojistaInvites.AsNoTracking()
            .Where(i => i.ParkingId == parkingId.Value)
            .ToDictionaryAsync(i => i.LojistaId, i => i, ct);

        var lojistas = await tenant.Lojistas.AsNoTracking().ToListAsync(ct);
        var walletByLojista = await tenant.LojistaWallets.AsNoTracking()
            .ToDictionaryAsync(w => w.LojistaId, w => w.BalanceHours, ct);

        var purchasedSums = await (
            from o in tenant.PackageOrders.AsNoTracking()
            join p in tenant.RechargePackages.AsNoTracking() on o.PackageId equals p.Id
            where o.Scope == "LOJISTA" && o.LojistaId != null && o.Status == "PAID"
            group p.Hours by o.LojistaId!.Value into g
            select new { LojistaId = g.Key, TotalHours = g.Sum() }
        ).ToDictionaryAsync(x => x.LojistaId, x => x.TotalHours, ct);

        var emailByLojista = await identity.Users.AsNoTracking()
            .Where(u => u.ParkingId == parkingId && u.Role == UserRole.LOJISTA && u.EntityId != null)
            .ToDictionaryAsync(u => u.EntityId!.Value, u => u.Email, ct);

        var items = lojistas
            .Select(loj =>
            {
                inviteByLojista.TryGetValue(loj.Id, out var inv);
                var activated = inv?.ActivatedAt != null || (inv is null && emailByLojista.ContainsKey(loj.Id));

                int? balance = null;
                int? purchased = null;
                string? email = null;
                if (activated)
                {
                    email = emailByLojista.GetValueOrDefault(loj.Id);
                    purchased = purchasedSums.GetValueOrDefault(loj.Id, 0);
                    balance = walletByLojista.GetValueOrDefault(loj.Id, 0);
                }

                return new
                {
                    merchantCode = inv?.MerchantCode,
                    lojistaId = loj.Id,
                    shopName = loj.Name,
                    createdAt = inv?.CreatedAt,
                    activated,
                    email,
                    totalPurchasedHours = purchased,
                    balanceHours = balance,
                };
            })
            .OrderByDescending(x => x.createdAt ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.shopName)
            .ToList();

        return Ok(new { items });
    }

    public sealed record CreateLojistaInviteRequest(string? DisplayName);
}
