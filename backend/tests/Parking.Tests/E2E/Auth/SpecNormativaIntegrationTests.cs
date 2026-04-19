using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Parking.Domain;
using Parking.Infrastructure.Auth;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Security;
using Parking.Infrastructure.Tenants;
using Parking.Tests.E2E.Infrastructure;
using Xunit;

namespace Parking.Tests.E2E.Auth;

/// <summary>SPEC §24 <c>Auth</c> — §7 OPERATOR_BLOCKED, §17 unsuspend, §18 recharge-packages scope.</summary>
[Collection(nameof(PostgresApiCollection))]
public sealed class SpecNormativaIntegrationTests(PostgresWebAppFixture fx)
{
    [Fact]
    public async Task Login_operador_com_mais_de_3_problem_no_dia_UTC_retorna_OPERATOR_BLOCKED()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var opId = Guid.NewGuid();
        var opEmail = $"op_prob_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            identity.Users.Add(new ParkingIdentityUser
            {
                Id = opId,
                Email = opEmail,
                PasswordHash = Argon2PasswordHasher.Hash("Op!12345"),
                Role = UserRole.OPERATOR,
                ParkingId = parkingId,
                EntityId = null,
                Active = true,
                OperatorSuspended = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await identity.SaveChangesAsync();
        }

        var now = DateTimeOffset.UtcNow;
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            for (var i = 0; i < 4; i++)
            {
                db.OperatorEvents.Add(new OperatorEventRow
                {
                    Id = Guid.NewGuid(),
                    UserId = opId,
                    Type = "PROBLEM",
                    CreatedAt = dayStart.AddMinutes(i + 1)
                });
            }

            await db.SaveChangesAsync();
        }

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = opEmail, password = "Op!12345" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var j = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OPERATOR_BLOCKED", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Refresh_operador_apos_exceder_limite_problem_diario_retorna_OPERATOR_BLOCKED()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var opId = Guid.NewGuid();
        var opEmail = $"op_ref_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            identity.Users.Add(new ParkingIdentityUser
            {
                Id = opId,
                Email = opEmail,
                PasswordHash = Argon2PasswordHasher.Hash("Op!12345"),
                Role = UserRole.OPERATOR,
                ParkingId = parkingId,
                EntityId = null,
                Active = true,
                OperatorSuspended = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await identity.SaveChangesAsync();
        }

        var login1 = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = opEmail, password = "Op!12345" });
        login1.EnsureSuccessStatusCode();
        var tok1 = await login1.Content.ReadFromJsonAsync<JsonElement>();
        var refresh = tok1.GetProperty("refresh_token").GetString()!;

        var now = DateTimeOffset.UtcNow;
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            for (var i = 0; i < 4; i++)
            {
                db.OperatorEvents.Add(new OperatorEventRow
                {
                    Id = Guid.NewGuid(),
                    UserId = opId,
                    Type = "PROBLEM",
                    CreatedAt = dayStart.AddMinutes(10 + i)
                });
            }

            await db.SaveChangesAsync();
        }

        var refreshRes = await http.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
        var j = await refreshRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OPERATOR_BLOCKED", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Admin_de_outro_parking_nao_pode_unsuspend_operador()
    {
        var http = fx.Factory.CreateClient();
        var (parkingA, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var (_, adminBTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var opId = Guid.NewGuid();
        var opEmail = $"op_sus_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            identity.Users.Add(new ParkingIdentityUser
            {
                Id = opId,
                Email = opEmail,
                PasswordHash = Argon2PasswordHasher.Hash("Op!12345"),
                Role = UserRole.OPERATOR,
                ParkingId = parkingA,
                EntityId = null,
                Active = true,
                OperatorSuspended = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await identity.SaveChangesAsync();
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/operators/{opId}/unsuspend");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminBTok);
        var res = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("FORBIDDEN", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GET_recharge_packages_sem_scope_valido_retorna_VALIDATION_ERROR()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var clientId = Guid.NewGuid();
        var cliEmail = $"cli_pkg_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            db.Clients.Add(new ClientRow { Id = clientId, Plate = "PKGSCO1", LojistaId = null });
            await db.SaveChangesAsync();
        }

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            identity.Users.Add(new ParkingIdentityUser
            {
                Id = Guid.NewGuid(),
                Email = cliEmail,
                PasswordHash = Argon2PasswordHasher.Hash("Cli!12345"),
                Role = UserRole.CLIENT,
                ParkingId = parkingId,
                EntityId = clientId,
                Active = true,
                OperatorSuspended = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await identity.SaveChangesAsync();
        }

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = cliEmail, password = "Cli!12345" });
        login.EnsureSuccessStatusCode();
        var cliTok = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        using var r1 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/recharge-packages");
        r1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cliTok);
        var noScope = await http.SendAsync(r1);
        Assert.Equal(HttpStatusCode.BadRequest, noScope.StatusCode);
        var j1 = await noScope.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_ERROR", j1.GetProperty("code").GetString());

        using var r2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/recharge-packages?scope=INVALID");
        r2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cliTok);
        var badScope = await http.SendAsync(r2);
        Assert.Equal(HttpStatusCode.BadRequest, badScope.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_reutilizado_revoga_sessoes_ativas_do_utilizador()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var email = $"adm_reuse_{Guid.NewGuid():N}@test.local";
        const string password = "Adm!12345";
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            identity.Users.Add(new ParkingIdentityUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = Argon2PasswordHasher.Hash(password),
                Role = UserRole.ADMIN,
                ParkingId = parkingId,
                EntityId = null,
                Active = true,
                OperatorSuspended = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await identity.SaveChangesAsync();
        }

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var t1 = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refresh_token").GetString()!;

        var refreshOk = await http.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = t1 });
        refreshOk.EnsureSuccessStatusCode();
        var t2 = (await refreshOk.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refresh_token").GetString()!;

        // Reuso de token já rotacionado deve invalidar a família ativa do usuário.
        var replay = await http.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = t1 });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        var afterReplay = await http.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = t2 });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
    }

    [Fact]
    public async Task Login_emite_refresh_token_com_janela_de_60_dias_por_padrao()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var email = $"adm_ttl_{Guid.NewGuid():N}@test.local";
        const string password = "Adm!12345";
        await using (var scopeSeed = fx.Factory.Services.CreateAsyncScope())
        {
            var identitySeed = scopeSeed.ServiceProvider.GetRequiredService<IdentityDbContext>();
            identitySeed.Users.Add(new ParkingIdentityUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = Argon2PasswordHasher.Hash(password),
                Role = UserRole.ADMIN,
                ParkingId = parkingId,
                EntityId = null,
                Active = true,
                OperatorSuspended = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await identitySeed.SaveChangesAsync();
        }

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var refresh = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refresh_token").GetString()!;
        var hash = JwtTokenService.HashRefreshToken(refresh);

        await using var scope = fx.Factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var row = await identity.RefreshTokens.AsNoTracking().FirstAsync(r => r.TokenHash == hash);
        var ttlDays = (row.ExpiresAt - DateTimeOffset.UtcNow).TotalDays;
        Assert.InRange(ttlDays, 59d, 61d);
    }
}
