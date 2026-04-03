using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Application.Lojistas;
using Parking.Domain;
using Parking.Infrastructure.Auth;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Security;
using Parking.Infrastructure.Tenants;

namespace Parking.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IdentityDbContext identity,
    JwtTokenService jwt,
    IOperatorProblemAuthCheck operatorProblemCheck,
    IConfiguration configuration,
    ITenantDbContextFactory tenantFactory) : ControllerBase
{
    private static readonly Dictionary<string, LoginThrottleState> Throttle = new(StringComparer.OrdinalIgnoreCase);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken ct)
    {
        var email = body.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Email obrigatório." });

        if (!CheckThrottle(email, out var retryAfter))
            return StatusCode(429, new { code = "LOGIN_THROTTLED", message = $"Aguarde {retryAfter}s." });

        var user = await identity.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
        if (user == null || !user.Active)
        {
            RegisterFailure(email);
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Credenciais inválidas." });
        }

        if (!Argon2PasswordHasher.Verify(body.Password, user.PasswordHash))
        {
            RegisterFailure(email);
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Credenciais inválidas." });
        }

        ClearThrottle(email);

        if (user.OperatorSuspended)
            return Unauthorized(new { code = "OPERATOR_BLOCKED", message = "Operador suspenso." });

        if (await operatorProblemCheck.ExceedsDailyProblemLimitAsync(user, ct))
            return Unauthorized(new { code = "OPERATOR_BLOCKED", message = "Limite de ocorrências PROBLEM excedido." });

        var refresh = JwtTokenService.NewOpaqueRefreshToken();
        var hash = JwtTokenService.HashRefreshToken(refresh);
        identity.RefreshTokens.Add(new RefreshTokenRow
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            Revoked = false
        });
        await identity.SaveChangesAsync(ct);

        var access = jwt.CreateAccessToken(user);
        return Ok(new { access_token = access, refresh_token = refresh, expires_in = 28800 });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest body, CancellationToken ct)
    {
        var opaque = body.RefreshToken;
        var hash = JwtTokenService.HashRefreshToken(opaque);
        var row = await identity.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash && !r.Revoked, ct);
        if (row == null || row.ExpiresAt < DateTimeOffset.UtcNow)
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Refresh inválido." });

        var user = await identity.Users.FirstOrDefaultAsync(u => u.Id == row.UserId, ct);
        if (user == null || !user.Active)
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Usuário inválido." });

        if (user.OperatorSuspended)
            return Unauthorized(new { code = "OPERATOR_BLOCKED", message = "Operador suspenso." });

        if (await operatorProblemCheck.ExceedsDailyProblemLimitAsync(user, ct))
            return Unauthorized(new { code = "OPERATOR_BLOCKED", message = "Limite de ocorrências PROBLEM excedido." });

        row.Revoked = true;
        var refresh = JwtTokenService.NewOpaqueRefreshToken();
        var newHash = JwtTokenService.HashRefreshToken(refresh);
        identity.RefreshTokens.Add(new RefreshTokenRow
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            Revoked = false
        });
        await identity.SaveChangesAsync(ct);

        var access = jwt.CreateAccessToken(user);
        return Ok(new { access_token = access, refresh_token = refresh, expires_in = 28800 });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest body, CancellationToken ct)
    {
        var hash = JwtTokenService.HashRefreshToken(body.RefreshToken);
        var row = await identity.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (row != null)
        {
            row.Revoked = true;
            await identity.SaveChangesAsync(ct);
        }

        return Ok(new { ok = true });
    }

    /// <summary>Auto cadastro LOJISTA com códigos emitidos pelo gestor (§ convites).</summary>
    [AllowAnonymous]
    [HttpPost("register-lojista")]
    public async Task<IActionResult> RegisterLojista([FromBody] RegisterLojistaRequest? body, CancellationToken ct)
    {
        if (body is null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Corpo JSON obrigatório." });

        var email = body.Email.Trim().ToLowerInvariant();
        var name = (body.Name ?? "").Trim();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(body.Password))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "E-mail, senha e nome são obrigatórios." });
        if (string.IsNullOrEmpty(name))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Nome é obrigatório." });

        var merchantRaw = (body.MerchantCode ?? "").Trim();
        if (merchantRaw.Length != 10 || string.IsNullOrEmpty(body.ActivationCode))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Código do lojista e código de ativação são obrigatórios." });

        var normalizedMerchant = merchantRaw.ToUpperInvariant();

        await using var idTx = await identity.Database.BeginTransactionAsync(ct);
        await identity.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM lojista_invites WHERE merchant_code = {normalizedMerchant} FOR UPDATE",
            cancellationToken: ct);

        var inv = await identity.LojistaInvites.FirstOrDefaultAsync(i => i.MerchantCode == normalizedMerchant, ct);
        if (inv is null ||
            !LojistaInviteCodes.TimingSafeEqualsHash(body.ActivationCode, inv.ActivationCodeHash))
        {
            await idTx.RollbackAsync(ct);
            return BadRequest(new { code = "LOJISTA_INVITE_INVALID", message = "Código do lojista ou ativação inválidos." });
        }

        if (inv.ActivatedAt is not null)
        {
            await idTx.RollbackAsync(ct);
            return Conflict(new { code = "LOJISTA_INVITE_CONSUMED", message = "Este convite já foi utilizado." });
        }

        if (await identity.Users.AnyAsync(u => u.Email.ToLower() == email, ct))
        {
            await idTx.RollbackAsync(ct);
            return Conflict(new { code = "CONFLICT", message = "E-mail já cadastrado." });
        }

        var template = configuration["TENANT_DATABASE_URL_TEMPLATE"]
                       ?? throw new InvalidOperationException("TENANT_DATABASE_URL_TEMPLATE required");
        var tenantCs = TenantConnectionStringBuilder.FromTemplate(template, inv.ParkingId);
        await using var tdb = tenantFactory.CreateReadWrite(tenantCs);
        var lojista = await tdb.Lojistas.FirstOrDefaultAsync(l => l.Id == inv.LojistaId, ct);
        if (lojista is null)
        {
            await idTx.RollbackAsync(ct);
            return StatusCode(500, new { code = "INTERNAL", message = "Dados do lojista inconsistentes." });
        }

        var oldName = lojista.Name;
        lojista.Name = name;
        try
        {
            await tdb.SaveChangesAsync(ct);
        }
        catch
        {
            await idTx.RollbackAsync(ct);
            throw;
        }

        var userId = Guid.NewGuid();
        identity.Users.Add(new ParkingIdentityUser
        {
            Id = userId,
            Email = email,
            PasswordHash = Argon2PasswordHasher.Hash(body.Password),
            Role = UserRole.LOJISTA,
            ParkingId = inv.ParkingId,
            EntityId = inv.LojistaId,
            Active = true,
            OperatorSuspended = false,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        inv.ActivatedAt = DateTimeOffset.UtcNow;
        inv.ActivatedUserId = userId;

        try
        {
            await identity.SaveChangesAsync(ct);
        }
        catch
        {
            lojista.Name = oldName;
            await tdb.SaveChangesAsync(ct);
            await idTx.RollbackAsync(ct);
            throw;
        }

        await idTx.CommitAsync(ct);

        var refresh = JwtTokenService.NewOpaqueRefreshToken();
        var hash = JwtTokenService.HashRefreshToken(refresh);
        identity.RefreshTokens.Add(new RefreshTokenRow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            Revoked = false,
        });
        await identity.SaveChangesAsync(ct);

        var user = await identity.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var access = jwt.CreateAccessToken(user);
        return Ok(new { access_token = access, refresh_token = refresh, expires_in = 28800 });
    }

    private static bool CheckThrottle(string email, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;
        lock (Throttle)
        {
            if (!Throttle.TryGetValue(email, out var st))
                return true;
            var window = TimeSpan.FromMinutes(15);
            if (DateTimeOffset.UtcNow - st.WindowStart > window)
            {
                Throttle.Remove(email);
                return true;
            }

            if (st.Failures >= 10)
            {
                var elapsed = DateTimeOffset.UtcNow - st.WindowStart;
                retryAfterSeconds = (int)Math.Max(1, (window - elapsed).TotalSeconds);
                return false;
            }

            return true;
        }
    }

    private static void RegisterFailure(string email)
    {
        lock (Throttle)
        {
            if (!Throttle.TryGetValue(email, out var st))
            {
                Throttle[email] = new LoginThrottleState { Failures = 1, WindowStart = DateTimeOffset.UtcNow };
                return;
            }

            var window = TimeSpan.FromMinutes(15);
            if (DateTimeOffset.UtcNow - st.WindowStart > window)
            {
                Throttle[email] = new LoginThrottleState { Failures = 1, WindowStart = DateTimeOffset.UtcNow };
                return;
            }

            st.Failures++;
        }
    }

    private static void ClearThrottle(string email)
    {
        lock (Throttle)
        {
            Throttle.Remove(email);
        }
    }

    private sealed class LoginThrottleState
    {
        public int Failures { get; set; }
        public DateTimeOffset WindowStart { get; set; }
    }

    public sealed record LoginRequest(string Email, string Password);
    public sealed record RefreshRequest(string RefreshToken);

    public sealed record RegisterLojistaRequest(
        string MerchantCode,
        string ActivationCode,
        string Email,
        string Password,
        string Name);
}
