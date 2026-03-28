using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Domain;
using Parking.Infrastructure.Auth;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Security;

namespace Parking.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IdentityDbContext identity,
    JwtTokenService jwt,
    IOperatorProblemAuthCheck operatorProblemCheck) : ControllerBase
{
    private static readonly Dictionary<string, LoginThrottleState> Throttle = new(StringComparer.OrdinalIgnoreCase);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken ct)
    {
        var email = body.Email.Trim();
        if (string.IsNullOrEmpty(email))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Email obrigatório." });

        if (!CheckThrottle(email, out var retryAfter))
            return StatusCode(429, new { code = "LOGIN_THROTTLED", message = $"Aguarde {retryAfter}s." });

        var user = await identity.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
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
}
