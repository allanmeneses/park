using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Security;
using Parking.Infrastructure.Tenants;

namespace Parking.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
public sealed class AdminController(
    IdentityDbContext identity,
    TenantProvisioner provisioner,
    IWebHostEnvironment env,
    IConfiguration configuration,
    ILogger<AdminController> log) : ControllerBase
{
    [Authorize(Roles = nameof(UserRole.SUPER_ADMIN))]
    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants(CancellationToken ct)
    {
        var items = await identity.Users
            .AsNoTracking()
            .Where(u => u.ParkingId != null && u.Role != UserRole.SUPER_ADMIN)
            .GroupBy(u => u.ParkingId!.Value)
            .Select(g => new
            {
                parkingId = g.Key,
                label = g.OrderBy(x => x.CreatedAt).Select(x => x.Email).FirstOrDefault() ?? g.Key.ToString()
            })
            .OrderBy(x => x.label)
            .ToListAsync(ct);

        return Ok(new { items });
    }

    [Authorize(Roles = nameof(UserRole.SUPER_ADMIN))]
    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.AdminEmail))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "adminEmail e adminPassword obrigatórios." });
        if (string.IsNullOrWhiteSpace(body.AdminPassword))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "adminPassword obrigatório." });

        var email = body.AdminEmail.Trim();
        var opEmail = body.OperatorEmail?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(opEmail))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "operatorEmail e operatorPassword obrigatórios." });
        if (string.IsNullOrWhiteSpace(body.OperatorPassword))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "operatorPassword obrigatório." });
        if (string.Equals(email, opEmail, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "E-mail do administrador e do operador devem ser diferentes." });

        if (await identity.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new { code = "CONFLICT", message = "Email já cadastrado." });
        if (await identity.Users.AnyAsync(u => u.Email == opEmail, ct))
            return Conflict(new { code = "CONFLICT", message = "E-mail do operador já cadastrado." });

        var parkingId = body.ParkingId ?? Guid.NewGuid();
        var identityCs = configuration["DATABASE_URL_IDENTITY"]
                         ?? throw new InvalidOperationException("DATABASE_URL_IDENTITY required");
        var adminCs = new NpgsqlConnectionStringBuilder(identityCs) { Database = "postgres" }.ConnectionString;

        try
        {
            await provisioner.CreateAndMigrateTenantAsync(adminCs, parkingId, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Tenant DB failed");
            return StatusCode(500, new { code = "INTERNAL", message = "Falha ao provisionar tenant." });
        }

        var template = configuration["TENANT_DATABASE_URL_TEMPLATE"]
                       ?? throw new InvalidOperationException("TENANT_DATABASE_URL_TEMPLATE required");
        var tenantCs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        await SeedPackagesAsync(tenantCs, ct);

        var adminId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        await using var idTx = await identity.Database.BeginTransactionAsync(ct);
        try
        {
            identity.Users.Add(new ParkingIdentityUser
            {
                Id = adminId,
                Email = email,
                PasswordHash = Argon2PasswordHasher.Hash(body.AdminPassword),
                Role = UserRole.ADMIN,
                ParkingId = parkingId,
                EntityId = null,
                Active = true,
                OperatorSuspended = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
            identity.Users.Add(new ParkingIdentityUser
            {
                Id = operatorId,
                Email = opEmail,
                PasswordHash = Argon2PasswordHasher.Hash(body.OperatorPassword),
                Role = UserRole.OPERATOR,
                ParkingId = parkingId,
                EntityId = null,
                Active = true,
                OperatorSuspended = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await identity.SaveChangesAsync(ct);
            await idTx.CommitAsync(ct);
        }
        catch
        {
            await idTx.RollbackAsync(ct);
            throw;
        }

        return Created(string.Empty, new
        {
            parkingId,
            databaseName = $"parking_{parkingId:N}",
            adminUserId = adminId,
            operatorUserId = operatorId
        });
    }

    private async Task SeedPackagesAsync(string tenantConnectionString, CancellationToken ct)
    {
        var seedPath = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "..", "database", "seed", "tenant_recharge_packages.sql"));
        if (!System.IO.File.Exists(seedPath))
        {
            log.LogWarning("Seed file not found: {Path}", seedPath);
            return;
        }

        var sql = await System.IO.File.ReadAllTextAsync(seedPath, ct);
        await using var conn = new NpgsqlConnection(tenantConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public sealed record CreateTenantRequest(
        Guid? ParkingId,
        string AdminEmail,
        string AdminPassword,
        string? OperatorEmail,
        string? OperatorPassword);

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpPost("operators/{userId:guid}/unsuspend")]
    public async Task<IActionResult> Unsuspend(Guid userId, CancellationToken ct)
    {
        var actorSub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(actorSub) || !Guid.TryParse(actorSub, out var actorId))
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Token inválido." });

        var actor = await identity.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorId, ct);
        if (actor == null)
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Usuário inválido." });

        var user = await identity.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
            return NotFound(new { code = "NOT_FOUND", message = "Usuário não encontrado." });

        if (actor.Role == UserRole.ADMIN)
        {
            if (user.ParkingId != actor.ParkingId)
                return StatusCode(403, new { code = "FORBIDDEN", message = "Operador não pertence ao seu estacionamento." });
        }

        user.OperatorSuspended = false;
        await identity.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }
}
