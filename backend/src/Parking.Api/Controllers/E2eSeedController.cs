using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Api.Parking;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Security;
using Parking.Infrastructure.Tenants;

namespace Parking.Api.Controllers;

/// <summary>
/// Apenas com <c>E2E_SEED=1</c> — dados para Playwright/CI (nunca expor em produção real).
/// </summary>
[ApiController]
[Route("api/v1/admin/e2e")]
public sealed class E2eSeedController(
    IConfiguration configuration,
    IdentityDbContext identity,
    ITenantDbContextFactory tenantFactory,
    IHttpContextAccessor httpAccessor) : ControllerBase
{
    private static readonly Guid ClientPkgId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid LojistaPkgId = Guid.Parse("22222222-2222-2222-2222-222222222201");

    private bool E2eEnabled =>
        string.Equals(configuration["E2E_SEED"], "1", StringComparison.Ordinal);

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpPost("client-with-history")]
    public async Task<IActionResult> ClientWithHistory([FromBody] E2eClientSeed body, CancellationToken ct)
    {
        if (!E2eEnabled)
            return NotFound();

        var email = body.Email.Trim();
        if (string.IsNullOrEmpty(email) || string.IsNullOrWhiteSpace(body.Password))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "email e password obrigatórios." });

        if (await identity.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new { code = "CONFLICT", message = "Email já cadastrado." });

        var http = httpAccessor.HttpContext!;
        var cs = http.Items[ParkingConstants.TenantConnectionStringItem] as string;
        if (string.IsNullOrEmpty(cs))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Tenant não resolvido." });

        var parkingId = (Guid)http.Items[ParkingConstants.ParkingIdItem]!;

        var clientId = Guid.NewGuid();
        var plate = $"ABC{Random.Shared.Next(1000, 9999)}";

        await using (var db = tenantFactory.CreateReadWrite(cs))
        {
            db.Clients.Add(new ClientRow { Id = clientId, Plate = plate, LojistaId = null });
            db.ClientWallets.Add(new ClientWalletRow
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                BalanceHours = 3,
                ExpirationDate = null
            });

            db.WalletLedger.Add(new WalletLedgerRow
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                LojistaId = null,
                DeltaHours = 5,
                Amount = 50m,
                PackageId = ClientPkgId,
                Settlement = "CREDIT",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-3)
            });

            var ticketId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            db.Tickets.Add(new TicketRow
            {
                Id = ticketId,
                Plate = plate,
                EntryTime = now.AddHours(-5),
                ExitTime = now.AddHours(-1),
                Status = TicketStatus.CLOSED,
                CreatedAt = now.AddHours(-5)
            });
            db.WalletUsages.Add(new WalletUsageRow
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                Source = "client",
                HoursUsed = 2
            });
            await db.SaveChangesAsync();
        }

        identity.Users.Add(new ParkingIdentityUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = Argon2PasswordHasher.Hash(body.Password),
            Role = UserRole.CLIENT,
            ParkingId = parkingId,
            EntityId = clientId,
            Active = true,
            OperatorSuspended = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await identity.SaveChangesAsync(ct);

        return Ok(new { ok = true, client_id = clientId, plate });
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpPost("lojista-with-history")]
    public async Task<IActionResult> LojistaWithHistory([FromBody] E2eClientSeed body, CancellationToken ct)
    {
        if (!E2eEnabled)
            return NotFound();

        var email = body.Email.Trim();
        if (string.IsNullOrEmpty(email) || string.IsNullOrWhiteSpace(body.Password))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "email e password obrigatórios." });

        if (await identity.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new { code = "CONFLICT", message = "Email já cadastrado." });

        var http = httpAccessor.HttpContext!;
        var cs = http.Items[ParkingConstants.TenantConnectionStringItem] as string;
        if (string.IsNullOrEmpty(cs))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Tenant não resolvido." });

        var parkingId = (Guid)http.Items[ParkingConstants.ParkingIdItem]!;

        var lojistaId = Guid.NewGuid();
        var plate = $"ABD{Random.Shared.Next(1000, 9999)}";

        await using (var db = tenantFactory.CreateReadWrite(cs))
        {
            db.Lojistas.Add(new LojistaRow { Id = lojistaId, Name = "E2E Lojista", HourPrice = 1m });
            db.LojistaWallets.Add(new LojistaWalletRow
            {
                Id = Guid.NewGuid(),
                LojistaId = lojistaId,
                BalanceHours = 18
            });

            var clientRowId = Guid.NewGuid();
            db.Clients.Add(new ClientRow { Id = clientRowId, Plate = plate, LojistaId = lojistaId });

            db.WalletLedger.Add(new WalletLedgerRow
            {
                Id = Guid.NewGuid(),
                ClientId = null,
                LojistaId = lojistaId,
                DeltaHours = 20,
                Amount = 100m,
                PackageId = LojistaPkgId,
                Settlement = "CREDIT",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-4)
            });

            var ticketId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            db.Tickets.Add(new TicketRow
            {
                Id = ticketId,
                Plate = plate,
                EntryTime = now.AddHours(-6),
                ExitTime = now.AddHours(-2),
                Status = TicketStatus.CLOSED,
                CreatedAt = now.AddHours(-6)
            });
            db.WalletUsages.Add(new WalletUsageRow
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                Source = "lojista",
                HoursUsed = 2
            });
            await db.SaveChangesAsync();
        }

        identity.Users.Add(new ParkingIdentityUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = Argon2PasswordHasher.Hash(body.Password),
            Role = UserRole.LOJISTA,
            ParkingId = parkingId,
            EntityId = lojistaId,
            Active = true,
            OperatorSuspended = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await identity.SaveChangesAsync(ct);

        return Ok(new { ok = true, lojista_id = lojistaId, plate });
    }

    public sealed record E2eClientSeed(string Email, string Password);
}
